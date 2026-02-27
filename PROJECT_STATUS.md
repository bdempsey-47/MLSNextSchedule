# MLS NEXT Schedule Ingestion — Project Status & Handoff

**Last Updated:** February 26, 2026  
**Status:** Foundation Complete — Ready for Functions & Frontend

---

## ✅ Completed

### 1. Solution & Project Structure
- **Solution:** `MLSNextSchedule.sln`
- **Class Libraries:**
  - `MLSNext.Data` — EF Core entities, DbContext, migrations
  - `MLSNext.Ingestion` — HTML parsing, API client, database upsert logic  - `MLSNext.Tests` — Unit & integration test suite (16/16 passing ✅)
- **Console Applications:**
  - `MLSNext.Verification` — Live API data verification tool for integration testing
### 2. Database Layer (`MLSNext.Data`)
**Entities created:**
- `Match` — Natural key: `MatchId` (from Modular11), includes all required fields
- `Team` — Home and Away team relationships to Match
- `Venue` — Field/stadium name
- `Division` — Competition division (e.g., Premier, Select)
- `Competition` — Competition type (e.g., AD)
- `AgeGroup` — Age brackets (U13, U15, U17, etc.)
- `RawIngestionLog` — Raw HTML storage for debugging/audit

**Key constraints:**
- UNIQUE constraint on `Match.MatchId` (enforced at both EF and DB level)
- UNIQUE indexes on reference table names (Teams, Venues, Divisions, etc.)
- Proper cascade/restrict delete behaviors

**Database migrations:**
- Initial migration generated: `Migrations/20260226183429_InitialCreate.cs`
- Migration files are ready but NOT applied (Azure SQL will handle this during deployment)

### 3. Ingestion Service Layer (`MLSNext.Ingestion`)

**Classes implemented:**

#### `Modular11Client`
- Builds query parameters for the Modular11 API
- Supports configurable: tournament ID, gender, age groups, match type, date ranges
- Throttles requests at 200ms between pages
- Handles pagination (1-indexed `open_page` parameter)

#### `ScheduleParser`
- Uses AngleSharp for HTML parsing
- **Targets only `visible-xs` containers** to avoid desktop/mobile duplication
- **NEW:** Implements team-associated score parsing from `<span class="score-match-table">` elements
- **Score format:** `"HOME_GOALS HOME_TEAM to AWAY_GOALS AWAY_TEAM"` (e.g., `"1 City SC Utah to 1 Phoenix Premier FC"`)
- Flexible score parsing: handles separators like `:`, `-`, `to`, `vs`
- Extracts 10 required fields per match: Match ID, Date, Teams, Age, Gender, Competition, Division, Venue, **Score**
- Returns `ParsedMatch` DTOs with all fields populated

#### `MatchUpsertService`
- Implements lookup-or-create pattern for reference tables (Teams, Venues, Divisions, etc.)
- Upserts Match records into database
- Handles duplicates gracefully
- Logs detailed statistics (new, updated, duplicate counts)

#### `IngestionOrchestrator`
- Full pagination loop starting at `open_page = 1`
- Stops when response contains `"No data available"` marker
- In-memory deduplication using `HashSet<string>` of Match IDs
- Stops after 3 consecutive empty pages (safety mechanism)
- Logs execution time and total match count per run

### 4. Build Status
✅ **All projects compile successfully with no errors or warnings**

### 5. Testing & Verification
- **Unit Tests:** 8/8 ScheduleParserTests passing ✅
- **Integration Tests:** 8/8 (5 MatchUpsertService + 3 IngestionOrchestrator) passing ✅
- **Total:** 16/16 tests passing ✅
- **MLSNext.Verification Console App:** Successfully retrieves and parses live Modular11 API data
  - Tested with Fall 2025 date range (Aug 1 - Dec 31, 2025)
  - Successfully parsed 25 real matches with all fields including team-associated scores
  - Example output: Match 18 — City SC Utah 1 vs Phoenix Premier FC 1

---

## 📂 Current Project Structure
```
MLSNextSchedule/
├── ChatGPT_TechnicalGuidelines.md     # Original requirements document
├── PROJECT_STATUS.md                  # This file
├── MLSNextSchedule.slnx               # Solution file
├── MLSNext.Data/
│   ├── AppDbContext.cs                # EF Core DbContext
│   ├── AppDbContextFactory.cs         # Design-time factory for migrations
│   ├── Entities/
│   │   ├── AgeGroup.cs
│   │   ├── Competition.cs
│   │   ├── Division.cs
│   │   ├── Match.cs
│   │   ├── Team.cs
│   │   ├── Venue.cs
│   │   └── RawIngestionLog.cs
│   ├── Migrations/
│   │   ├── 20260226183429_InitialCreate.cs
│   │   ├── 20260226183429_InitialCreate.Designer.cs
│   │   └── AppDbContextModelSnapshot.cs
│   └── MLSNext.Data.csproj
├── MLSNext.Ingestion/
│   ├── Models/
│   │   └── ParsedMatch.cs             # DTO for parsed match data
│   ├── Services/
│   │   ├── Modular11Client.cs         # HTTP client + settings (virtual FetchPageAsync)
│   │   ├── ScheduleParser.cs          # HTML parser with score extraction
│   │   ├── MatchUpsertService.cs      # Database upsert logic
│   │   └── IngestionOrchestrator.cs   # Orchestration + pagination
│   └── MLSNext.Ingestion.csproj
├── MLSNext.Tests/
│   ├── Unit/
│   │   └── ScheduleParserTests.cs     # 8 tests (all passing)
│   ├── Integration/
│   │   ├── MatchUpsertServiceIntegrationTests.cs  # 5 tests (all passing)
│   │   └── IngestionOrchestratorIntegrationTests.cs  # 3 tests (all passing)
│   ├── Fixtures/
│   │   └── ScheduleParserFixtures.cs
│   └── MLSNext.Tests.csproj
├── MLSNext.Verification/
│   ├── Program.cs                    # Console app for live API testing
│   ├── local.settings.json           # Configuration (Fall 2025 dates)
│   └── MLSNext.Verification.csproj
└── .gitignore                        # Updated to exclude output*.txt files
```

---

## 🎯 Remaining Work (In Priority Order)

### Phase 2: Azure Functions Host
**Project to create:** `MLSNext.Functions` (.NET 8 isolated worker)

**HTTP Trigger endpoints:**
```
GET  /api/matches          — Query params: team, startDate, endDate, ageGroup, division
GET  /api/teams
GET  /api/divisions
GET  /api/agegroups
POST /api/ingestion/trigger — Manually trigger an ingestion run (testing)
```

**Timer Trigger:**
```
ScheduledIngestion — Timer trigger on cron: 0 0 */4 * * * (every 4 hours)
```

**Configuration to wire in `local.settings.json`:**
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ConnectionStrings__DefaultConnection": "Server=(local);Database=MLSNext;Trusted_Connection=true;Encrypt=false;",
    "Modular11__TournamentId": "35",
    "Modular11__Gender": "1",
    "Modular11__Status": "scheduled",
    "Modular11__MatchType": "2",
    "Modular11__AgeGroups": "13,14,15,16,17,18",
    "Modular11__StartDate": "",
    "Modular11__EndDate": ""
  }
}
```

### Phase 3: React Frontend
**Project to create:** `mlsnext-web/` (outside .NET solution)

**Tech stack:** Vite + React + TypeScript

**Features:**
- Filter bar: Team, Age Group, Division dropdowns; date range picker
- Match card list: Home vs. Away, date/time, venue, score (or TBD)
- Responsive mobile-first layout (375px+)
- Consume `VITE_API_BASE_URL` environment variable

**Deployment:** Azure Static Web Apps (free tier)

### Phase 4: Azure Infrastructure
1. **Azure SQL Database** (free offer, 32 GB per subscription)
   - Create database from migration
   - Store connection string in Function App Application Settings

2. **Azure Function App** (Consumption Plan)
   - Deploy `MLSNext.Functions` project
   - Bind connection string and Modular11 settings as Application Settings

3. **Azure Static Web Apps** (free tier)
   - Deploy React build
   - Configure routing (SPA: all paths → index.html)
   - Set `VITE_API_BASE_URL` to Function App URL

---

## 🔧 Key Architecture Decisions

✅ **AngleSharp for HTML parsing** — Cleaner CSS-class selector support than HtmlAgilityPack  
✅ **Azure Functions Consumption Plan** — $0 hosting for ~180 invocations/month  
✅ **Lookup-or-create pattern** — Clean reference table management without pre-population  
✅ **Isolated worker model** — Current Microsoft-recommended pattern for .NET Functions  
✅ **`MLSNext.Ingestion` is Functions-agnostic** — All logic is unit-testable, no Azure dependency  

---

## 🚀 Quick Start Commands

```powershell
# Build entire solution
dotnet build

# Build specific project
dotnet build MLSNext.Data
dotnet build MLSNext.Ingestion

# Generate new migration (after schema changes)
dotnet ef migrations add <MigrationName> --project MLSNext.Data

# Create database locally (for testing)
dotnet ef database update --project MLSNext.Data
```

---

## ⚠️ Important Notes for Handoff

1. **Local SQL Server required for testing** — Update connection string in `AppDbContextFactory.cs` if needed
2. **Migration not applied yet** — Will be handled by deployment process to Azure SQL
3. **No secrets in code** — All sensitive config (API keys, connection strings) go to Azure App Settings
4. **Modular11 API is undocumented** — HTML parsing is the only option; response format may change
5. **Rate limiting not enforced** — Polite throttling at 200ms per request is built in

---

## 📋 Verification Checklist Before Next Phase

**Data Layer & Parsing:**
- [x] MLSNext.Data compiles without errors
- [x] MLSNext.Ingestion compiles without errors
- [x] All entity relationships defined correctly
- [x] Migrations generated successfully
- [x] Parser targets mobile markup only (`visible-xs`)
- [x] Parser correctly extracts scores with team association
- [x] Orchestrator implements pagination correctly
- [x] Upsert service handles duplicates gracefully

**Testing:**
- [x] All 16 unit & integration tests passing (8 + 5 + 3)
- [x] Live API verification successful with Fall 2025 data (25 matches parsed)
- [x] Score parsing working correctly with team association
- [x] Moq virtual method setup fixed (Modular11Client.FetchPageAsync)

---

## 📞 Contact / Questions

If picking up this project in a new session:
1. Review this file for current state
2. Check `ChatGPT_TechnicalGuidelines.md` for detailed API specs
3. All business logic is in `MLSNext.Ingestion` — start there if debugging parsing or upsert issues
4. All schema is in `MLSNext.Data/Entities/` — start there if database questions arise

---

## 🔄 Latest Session Summary (Feb 26, 2026)

**What was completed:**
1. **Score Parsing Implementation**
   - Created `ExtractScoreWithTeamAssociation()` method in ScheduleParser
   - Scores now extracted from `<span class="score-match-table">` (visual score element, not details block)
   - Implemented flexible format handling: `:`, `-`, `to`, `vs` separators
   - Score format: `"HOME_GOALS HOME_TEAM to AWAY_GOALS AWAY_TEAM"` (e.g., "1 City SC Utah to 1 Phoenix Premier FC")

2. **Test Suite Fixes**
   - Updated all ScheduleParser test fixtures to include score span elements
   - Made `Modular11Client.FetchPageAsync()` virtual to allow proper Moq mocking
   - Fixed 3 failing integration tests (IngestionOrchestratorIntegrationTests)
   - All 16 tests now passing ✅

3. **Live API Verification**
   - Created `MLSNext.Verification` console app for live API testing
   - Verified Fall 2025 data (Aug 1 - Dec 31, 2025): 25 matches successfully parsed
   - Confirmed all fields including team-associated scores retrieving correctly

4. **Git Commits**
   - Commit 815c005: "feat: implement team-associated score parsing from Modular11 API"
   - Commit ecb0bed: "chore: add output.txt files to gitignore"

**Next Steps:**
1. Phase 2: Create `MLSNext.Functions` Azure Functions project
2. Implement HTTP GET/POST endpoints for match queries
3. Implement timer-triggered scheduled ingestion (every 4 hours)
4. Phase 3: Create React frontend
5. Phase 4: Azure infrastructure setup
