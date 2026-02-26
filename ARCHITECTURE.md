# MLSNextSchedule — Architecture & File Reference

**Last Updated:** February 26, 2026  
**Status:** Phase 2 Complete (Functions Host scaffolded and compiling)

---

## 📋 Project Overview

Three-tier architecture:
- **Data Layer** (`MLSNext.Data`) — EF Core DbContext, entities, migrations
- **Ingestion Layer** (`MLSNext.Ingestion`) — HTML parsing, API client, database orchestration
- **Functions Host** (`MLSNext.Functions`) — Azure Functions with HTTP and Timer triggers

All projects target `.NET 10.0`.

---

## 🏗️ Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Azure Functions Host                           │
│               (MLSNext.Functions - Isolated Worker)                 │
├─────────────────────────────────────────────────────────────────────┤
│  HTTP Triggers (GET/POST)              │  Timer Trigger            │
│  ├─ GetMatches                         │  └─ ScheduledIngestion    │
│  ├─ GetTeams                           │     (every 4 hours)       │
│  ├─ GetDivisions                       │                           │
│  ├─ GetAgeGroups                       │                           │
│  └─ TriggerIngestion (manual)          │                           │
└──────────────────┬──────────────────────────────────────────────────┘
                   │ uses
┌──────────────────▼──────────────────────────────────────────────────┐
│              Ingestion Service Layer                                 │
│          (MLSNext.Ingestion - .NET 10 Library)                      │
├─────────────────────────────────────────────────────────────────────┤
│  IngestionOrchestrator   ← Main orchestrator                        │
│  ├─ Modular11Client      ← HTTP client for Modular11 API           │
│  ├─ ScheduleParser       ← HTML parsing with AngleSharp           │
│  └─ MatchUpsertService   ← Database upsert logic                   │
│                                                                      │
│  Models:                                                             │
│  └─ ParsedMatch (DTO)    ← In-memory parsed match data             │
└──────────────────┬──────────────────────────────────────────────────┘
                   │ uses
┌──────────────────▼──────────────────────────────────────────────────┐
│              Data Access Layer                                       │
│          (MLSNext.Data - .NET 10 Library)                           │
├─────────────────────────────────────────────────────────────────────┤
│  AppDbContext            ← EF Core DbContext                       │
│  ├─ Entities:                                                       │
│  │  ├─ Match              ← Natural key: MatchId (from Modular11)  │
│  │  ├─ Team               ← Home/Away team references              │
│  │  ├─ Venue              ← Field/stadium location                 │
│  │  ├─ Division           ← Competition division (Premier, etc)    │
│  │  ├─ Competition        ← Competition type (AD, etc)            │
│  │  ├─ AgeGroup           ← Age brackets (U13, U15, etc)          │
│  │  └─ RawIngestionLog    ← Raw HTML audit trail                  │
│  │                                                                  │
│  └─ Migrations:                                                     │
│     └─ 20260226183429_InitialCreate                                │
│                                                                      │
│  AppDbContextFactory     ← Design-time factory for migrations      │
└──────────────────┬──────────────────────────────────────────────────┘
                   │
                   ▼
           SQL Server Database
```

---

## 📁 File-by-File Reference

### **MLSNext.Data** (Data Access Layer)

#### Core Classes

**AppDbContext.cs**
- **Purpose:** EF Core DbContext for all database operations
- **Key Members:**
  - `DbSet<Match>`
  - `DbSet<Team>`
  - `DbSet<Venue>`
  - `DbSet<Division>`
  - `DbSet<Competition>`
  - `DbSet<AgeGroup>`
  - `DbSet<RawIngestionLog>`
- **Configuration:** Entity relationships, unique constraints, cascade delete rules defined in `OnModelCreating()`
- **Natural Key:** Match uses `MatchId` as natural primary key (source identifier from Modular11)

**AppDbContextFactory.cs**
- **Purpose:** Design-time factory for EF Core migrations
- **Usage:** `dotnet ef migrations add` commands rely on this to instantiate DbContext
- **Configuration:** Local SQL Server connection string (hardcoded for development; production uses Azure App Settings)
- **Key Method:** `CreateDbContext(string[] args)` returns configured DbContext instance

#### Entity Classes

**Match.cs**
- **Natural Key:** `MatchId` (string, from Modular11, UNIQUE constraint)
- **Properties:**
  - `MatchDateUtc` — Scheduled match date/time
  - `Score` — Final or TBD score (e.g., "2-1")
  - `Gender` — Male/Female/Mixed
  - `CreatedAt`, `UpdatedAt` — Audit timestamps
- **Foreign Keys:** HomeTeamId, AwayTeamId, VenueId, DivisionId, CompetitionId, AgeGroupId
- **Navigation Properties:** HomeTeam, AwayTeam, Venue, Division, Competition, AgeGroup
- **Uniqueness:** `MatchId` is UNIQUE at both EF and database level

**Team.cs**
- **Key:** `Id` (int, auto-increment)
- **Properties:** `Name` (string, UNIQUE)
- **Navigation:** Referenced by Match (HomeTeam, AwayTeam)
- **Cascade:** Delete restricted (Teams not deleted when Matches deleted)

**Venue.cs**
- **Key:** `Id` (int)
- **Properties:** `Name` (string, UNIQUE)
- **Use:** Stores field/stadium names

**Division.cs**
- **Key:** `Id` (int)
- **Properties:** `Name` (string, UNIQUE)
- **Examples:** "Premier", "Select", "Competitive"

**Competition.cs**
- **Key:** `Id` (int)
- **Properties:** `Name` (string, UNIQUE)
- **Examples:** "AD" (Arena Duals), league codes

**AgeGroup.cs**
- **Key:** `Id` (int)
- **Properties:** `Name` (string, UNIQUE)
- **Examples:** "U13", "U15", "U17", "U19"

**RawIngestionLog.cs**
- **Purpose:** Audit trail for debugging and replay capability
- **Key:** `Id` (int)
- **Properties:**
  - `RawHtmlContent` — Full HTML response from Modular11
  - `ParsedMatchCount` — Number of matches extracted
  - `CreatedAt` — Ingestion timestamp
  - `PageNumber` — Which API page was fetched
  - `Status` — "Success", "Error", etc.

#### Migrations

**Migrations/20260226183429_InitialCreate.cs**
- Initial schema creation for all tables
- Sets up UNIQUE constraints on natural keys and reference table names
- Establishes foreign key relationships with appropriate delete behaviors
- **Not yet applied** — Deployment process handles database creation on Azure SQL

**Migrations/AppDbContextModelSnapshot.cs**
- EF-generated metadata snapshot of current schema (used for future migrations)

---

### **MLSNext.Ingestion** (Ingestion Services Layer)

#### Core Orchestrator

**Services/IngestionOrchestrator.cs**
- **Purpose:** Master orchestrator for the entire ingestion pipeline
- **Key Method:** `RunAsync(CancellationToken ct)` → Async, long-running job
- **Workflow:**
  1. Loop starting at `open_page = 1`
  2. Fetch page via `Modular11Client.FetchPageAsync()`
  3. Parse HTML via `ScheduleParser.ParseMatches()`
  4. In-memory deduplication using `HashSet<string>` of MatchIds
  5. Upsert via `MatchUpsertService.UpsertMatchesAsync()`
  6. Loop until "No data available" or 3 consecutive empty pages
- **Logging:** Records execution time, total match count, page-by-page progress
- **Error Handling:** Propagates exceptions (caller handles retry logic)
- **Constructor Injection:**
  - `Modular11Client`
  - `ScheduleParser`
  - `MatchUpsertService`
  - `AppDbContext`
  - `ILogger<IngestionOrchestrator>`

#### API Client

**Services/Modular11Client.cs**
- **Purpose:** HTTP client for Modular11 API pagination
- **Key Method:** `FetchPageAsync(int pageNumber, CancellationToken ct)` → Returns raw HTML string
- **Features:**
  - 200ms throttle between requests (rate limiting)
  - Query parameter building with `BuildQueryParams(int pageNumber)`
  - Error logging and exception propagation
  - Supports configurable: tournament ID, gender, age groups, match type, date ranges
- **Settings:** Injected `Modular11Settings` via constructor
- **HTTP Client:** Uses injected `HttpClient` (factory pattern from DI)
- **Query Parameters:**
  - `tournament` — Tournament ID (e.g., "35")
  - `gender` — 1=Male, etc.
  - `status` — "scheduled", "completed"
  - `match_type` — Match type code (e.g., "2")
  - `age[]` — Repeatable age group parameter (e.g., age[]=13, age[]=14)
  - `open_page` — 1-indexed page number
  - `start_date`, `end_date` — Optional date range

**Modular11Settings Class**
- **Purpose:** Configuration container (similar to Options pattern)
- **Properties:**
  - `TournamentId` (required)
  - `Gender` (required)
  - `Status` (required)
  - `MatchType` (required)
  - `AgeGroups` (required, `List<string>`)
  - `StartDate` (optional)
  - `EndDate` (optional)
- **Bound from:** `appsettings.json` or Azure App Settings under `"Modular11"` section

#### HTML Parser

**Services/ScheduleParser.cs**
- **Purpose:** Parse HTML response from Modular11 API using AngleSharp
- **Key Method:** `ParseMatches(string htmlContent)` → Returns `List<ParsedMatch>`
- **Target:** Mobile markup only (`visible-xs` CSS class containers)
  - Reason: Avoids parsing desktop/tablet duplicates
  - Uses AngleSharp QuerySelectorAll for CSS selectors
- **Extraction:** 10 fields per match
  1. MatchId (global unique identifier)
  2. MatchDate (scheduled date/time)
  3. HomeTeam (team name)
  4. AwayTeam (team name)
  5. Age (e.g., "U13")
  6. Gender (Male/Female/Mixed)
  7. Division (e.g., "Premier")
  8. Competition (e.g., "AD")
  9. Venue (field/stadium name)
  10. Score (final score or "TBD")
- **Error Handling:** Logs parse errors per match without stopping entire batch
- **Dependencies:** AngleSharp NuGet package

#### Database Upsert Service

**Services/MatchUpsertService.cs**
- **Purpose:** Handle lookup-or-create pattern for reference tables and upsert Match records
- **Key Method:** `UpsertMatchesAsync(List<ParsedMatch> matches, CancellationToken ct)`
- **Workflow for Each Match:**
  1. Look up or create Team records (HomeTeam, AwayTeam)
  2. Look up or create Venue record
  3. Look up or create Division record
  4. Look up or create Competition record
  5. Look up or create AgeGroup record
  6. Upsert Match record using natural key `MatchId`
- **Lookup Logic:** `FirstOrDefaultAsync()` by name, create if not found
- **Upsert Logic:**
  - If Match with MatchId exists: Update `Score`, `UpdatedAt`, other changed fields
  - If Match doesn't exist: Insert new record with `CreatedAt = now`
- **Deduplication:** Gracefully handles duplicate submission (idempotent)
- **Logging:** Records counts of new, updated, and skipped (duplicate) matches
- **Database Transaction:** Single `SaveChangesAsync()` at end (batch efficiency)
- **Error Handling:** Logs validation/constraint errors

#### Data Transfer Object

**Models/ParsedMatch.cs**
- **Purpose:** In-memory DTO for parsed match data (no database identity)
- **Properties:** Match the 10 extracted fields from HTML
  - `MatchId` (string)
  - `MatchDate` (DateTime)
  - `HomeTeamName` (string)
  - `AwayTeamName` (string)
  - `AgeGroup` (string)
  - `Gender` (string)
  - `Division` (string)
  - `Competition` (string)
  - `Venue` (string)
  - `Score` (string or "TBD")
- **Usage:** Passed from Parser → MatchUpsertService; not persisted directly

---

### **MLSNext.Functions** (Azure Functions Host)

#### Bootstrap & Configuration

**Program.cs**
- **Purpose:** Application entry point and dependency injection setup
- **Key Steps:**
  1. Create `HostBuilder` with isolated worker model
  2. `ConfigureServices()` — Register all DI containers
  3. `ConfigureFunctionsWorkerDefaults()` — Azure Functions runtime configuration
  4. `Build()` → `Run()` — Start the host
- **Configuration Loading:**
  - Read `local.settings.json` (development)
  - Fall back to `Environment` variables (Azure App Settings)
- **Service Registrations:**
  - `DbContext` → Scoped (one per request)
  - `Modular11Settings` → Singleton (parsed from config)
  - `Modular11Client` → Scoped
  - `ScheduleParser` → Scoped
  - `MatchUpsertService` → Scoped
  - `IngestionOrchestrator` → Scoped
  - `HttpClient` (factory) → Named client for Modular11Client
- **Logging:** Uses ILogger from DI
- **Error Handling:** Propagates exceptions to runtime

#### HTTP Trigger: GetMatches

**Triggers/GetMatches.cs**
- **Route:** `GET /api/matches`
- **Query Parameters:**
  - `team` — Filter by team name (substring match)
  - `startDate` — Filter by date >= (format: "YYYY-MM-DD")
  - `endDate` — Filter by date <= (format: "YYYY-MM-DD")
  - `ageGroup` — Filter by age group name (exact match)
  - `division` — Filter by division name (exact match)
- **Behavior:**
  - Returns up to 100 matches ordered by date ascending
  - Includes navigation properties: HomeTeam, AwayTeam, Venue, AgeGroup, Division, Competition
  - Returns HTTP 200 with JSON array
  - Returns HTTP 500 with error details on exception
- **Example Response:**
  ```json
  [
    {
      "matchId": "m-12345",
      "matchDateUtc": "2026-03-15T14:00:00Z",
      "homeTeam": {"id": 1, "name": "Dragons FC"},
      "awayTeam": {"id": 2, "name": "Phoenix United"},
      "venue": {"id": 1, "name": "Central Park"},
      "score": "2-1",
      ...
    }
  ]
  ```

#### HTTP Trigger: GetTeams

**Triggers/GetTeams.cs**
- **Route:** `GET /api/teams`
- **Behavior:**
  - Returns all teams ordered by name ascending
  - Returns Id and Name only (projection)
  - Returns HTTP 200 with JSON array
  - Returns HTTP 500 on error
- **Example Response:**
  ```json
  [
    {"id": 1, "name": "Dragons FC"},
    {"id": 2, "name": "Phoenix United"}
  ]
  ```

#### HTTP Trigger: GetDivisions

**Triggers/GetDivisions.cs**
- **Route:** `GET /api/divisions`
- **Behavior:**
  - Returns all divisions ordered by name ascending
  - Returns Id and Name only
  - Returns HTTP 200 with JSON array
- **Example Response:**
  ```json
  [
    {"id": 1, "name": "Premier"},
    {"id": 2, "name": "Select"}
  ]
  ```

#### HTTP Trigger: GetAgeGroups

**Triggers/GetAgeGroups.cs**
- **Route:** `GET /api/agegroups`
- **Behavior:**
  - Returns all age groups ordered by name ascending
  - Returns Id and Name only
  - Returns HTTP 200 with JSON array
- **Example Response:**
  ```json
  [
    {"id": 1, "name": "U13"},
    {"id": 2, "name": "U15"},
    {"id": 3, "name": "U17"}
  ]
  ```

#### HTTP Trigger: TriggerIngestion (Manual)

**Triggers/TriggerIngestion.cs**
- **Route:** `POST /api/ingestion/trigger`
- **Authorization:** Anonymous (testing; should be restricted in production)
- **Behavior:**
  1. Calls `IngestionOrchestrator.RunAsync()` directly
  2. Measures execution time
  3. Returns HTTP 200 with summary on success
  4. Returns HTTP 500 with error message on failure
- **Response (Success):**
  ```json
  {
    "success": true,
    "message": "Ingestion completed",
    "executionTimeMs": 45230
  }
  ```
- **Response (Error):**
  ```json
  {
    "error": "Connection timeout"
  }
  ```
- **Use Case:** Manual testing, debugging, or one-off ingestion runs
- **Note:** In production, should require Function Key or Azure AD authentication

#### Timer Trigger: ScheduledIngestion

**Triggers/ScheduledIngestion.cs**
- **Schedule:** CRON `"0 0 */4 * * *"` (every 4 hours at minute 0)
- **Behavior:**
  1. Logs ingestion start with UTC timestamp
  2. Calls `IngestionOrchestrator.RunAsync()`
  3. Measures and logs execution time
  4. Logs warning if run is behind schedule (`IsPastDue`)
  5. Re-throws exceptions for Azure Functions monitoring
- **Logging Output:**
  ```
  Scheduled ingestion started at 2026-02-26T12:00:00.0000000Z
  Ingestion completed in 45230.5ms
  ```
- **Error Handling:** Logs full stack trace; Azure Functions handles retry/alerting
- **Note:** Can be modified to run at different intervals (e.g., hourly, daily)

---

## 📦 Project Dependencies

### MLSNext.Data
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.x" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.x" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.x" />
</ItemGroup>
```

### MLSNext.Ingestion
```xml
<ItemGroup>
  <PackageReference Include="AngleSharp" Version="1.x" />
  <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.3" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.x" />
</ItemGroup>
```

### MLSNext.Functions
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.51.0" />
  <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" Version="3.1.0" />
  <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Timer" Version="4.1.0" />
  <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.3" />
  <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="10.0.0" />
</ItemGroup>
```

---

## 🔄 Data Flow

### Ingestion Pipeline
```
[Modular11Client]
    ↓ HTTP GET
[Modular11 Website]
    ↓ Raw HTML
[ScheduleParser]
    ↓ List<ParsedMatch>
[MatchUpsertService]
    ↓ SQL INSERT/UPDATE
[AppDbContext]
    ↓
[SQL Server Database]
```

### Query Pipeline
```
[HTTP Client Request]
    ↓ (e.g., GET /api/matches?team=Dragons)
[GetMatches Function]
    ↓ (Query builder)
[AppDbContext]
    ↓ (LINQ to SQL)
[SQL Server]
    ↓ (result set)
[AppDbContext] (materialization)
    ↓ (JSON serialization)
[HTTP Response (200 OK)]
```

---

## 🎯 Key Design Patterns

| Pattern | Usage | File |
|---------|-------|------|
| **Repository** | Data access abstraction via EF Core DbContext | `AppDbContext.cs` |
| **Factory** | DbContext creation for migrations | `AppDbContextFactory.cs` |
| **Dependency Injection** | Service registration and resolution | `Program.cs` |
| **Options/Settings** | Configuration container | `Modular11Settings` in `Modular11Client.cs` |
| **HTTP Client Factory** | Reusable HTTP clients with DI | `Program.cs` (AddHttpClient) |
| **Data Transfer Object (DTO)** | In-memory match data | `ParsedMatch.cs` |
| **Orchestrator** | Multi-step workflow coordination | `IngestionOrchestrator.cs` |
| **Lookup-or-Create** | Idempotent reference table population | `MatchUpsertService.cs` |
| **Upsert** | Insert-or-update pattern | `MatchUpsertService.cs` |
| **In-Memory Deduplication** | HashSet-based duplicate detection | `IngestionOrchestrator.cs` |

---

## 🚀 Running the Functions Locally

```powershell
# Install Azure Functions Core Tools (one-time)
# https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local

# Start the functions host
cd MLSNext.Functions
func start

# Or with .NET CLI
dotnet build
dotnet run

# Test endpoints
curl http://localhost:7071/api/teams
curl "http://localhost:7071/api/matches?team=Dragons"
curl -X POST http://localhost:7071/api/ingestion/trigger
```

---

## 🔐 Configuration (local.settings.json)

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(local);Database=MLSNext;Trusted_Connection=true;Encrypt=false;"
  },
  "Modular11": {
    "TournamentId": "35",
    "Gender": "1",
    "Status": "scheduled",
    "MatchType": "2",
    "AgeGroups": "13,14,15,16,17,18",
    "StartDate": "",
    "EndDate": ""
  }
}
```

---

## 📊 Next Steps

Phase 3: React Frontend (`mlsnext-web/`)
- Query filters (team, age group, division, date range)
- Match card list display
- Responsive design (mobile-first)

Phase 4: Azure Deployment
- Provision Azure SQL Database
- Deploy Function App (Consumption Plan)
- Deploy Static Web Apps (React build)
