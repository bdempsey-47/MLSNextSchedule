# Youth Soccer Schedules (YSS) — Project Status

**Last Updated:** March 2, 2026  
**Status:** Phase 3 Complete, Phase 4 Ready to Begin

---

## 🎯 Current State

### ✅ Phases Complete

**Phase 1 & 2 — Backend & Data Layer** ✅
- Database schema: League → Division → Region → Match hierarchy
- Ingestion pipeline: HTML parsing, API calls, database upsert
- Azure Functions: 5 HTTP endpoints + scheduled timer trigger
- Testing: 36/36 unit & integration tests passing
- Build: All projects compile with 0 errors

**Phase 3 — React Frontend** ✅
- React 18 + TypeScript + Vite application
- 5 core components (ProgramSelector, SeasonSelector, FilterBar, MatchList, MatchCard)
- Multi-select filters with URL bookmarking
- Team logos, Google Maps venue links, calendar export (.ics)
- Responsive design (mobile-first, 375px+)
- 100 sample matches with 104 teams loaded in LocalDB

### 📊 Architecture

```
Frontend (React)              Backend (Azure Functions)      Database (SQL)
├─ ProgramSelector ✅        ├─ GetMatches ✅              ├─ Leagues
├─ SeasonSelector ✅         ├─ GetTeams ✅                ├─ Divisions
├─ FilterBar ✅              ├─ GetDivisions ✅            ├─ Regions
├─ MatchList ✅              ├─ GetRegions ✅              ├─ Matches
└─ MatchCard ✅              ├─ GetAgeGroups ✅            ├─ Teams
                             ├─ TriggerIngestion ✅        ├─ Venues
                             └─ ScheduledIngestion ✅      ├─ AgeGroups
                                                          └─ Competitions
```

### 🔧 Key Features Implemented

**Data Layer:**
- Multi-league support (MLS Next, ECNL, EDP seeded but only MLS Next data ingested)
- League-configurable ingestion (all services accept `leagueName` parameter)
- EF Core lookup-or-create pattern for reference tables
- League filtering on API endpoints

**Frontend:**
- Multi-select programs (Homegrown + Academy simultaneously)
- Multi-select seasons (Fall + Spring)
- Context-aware team autocomplete (scoped to selected filters)
- Clickable team names, age groups, regions (direct filter trigger)
- Clickable team logos (same action as team name)
- Team logo display with initials fallback
- Google Maps venue search links with disclaimer tooltip
- Calendar export to system default calendar app (.ics download)
- URL bookmarking (all filter state persisted in query string)
- Responsive grid layout with consistent card heights

**Backend:**
- Configurable league name parameter (defaults to "MLS Next")
- Repeatable query parameters for multi-select filters
- League filtering in GetMatches and GetTeams endpoints
- CORS configured for localhost development

---

## 🚀 Quick Start

### Local Development (Full Stack)

**Terminal 1 — Backend API:**
```powershell
cd YSS.Functions
func start --functions GetMatches GetTeams GetDivisions GetRegions GetAgeGroups TriggerIngestion
# Runs on http://localhost:7071
```

**Terminal 2 — Frontend:**
```powershell
cd YSS.Web
npm run dev
# Runs on http://localhost:5173
```

### Re-ingest Sample Data

```powershell
cd YSS.Verification
dotnet run
# Output: Matches: 100 | Teams: 104 | With logos: 104
# Clears tables (except Leagues) and re-ingests from Modular11 API
```

### Build & Test

```powershell
dotnet build                    # Build entire solution
dotnet test YSS.Tests       # Run all 36 tests
dotnet ef migrations add <Name> --project YSS.Data  # New migration
dotnet ef database update       # Apply migrations to LocalDB
```

---

## 📋 Next Steps (Priority Order)

### Phase 4 — Azure Deployment

- [ ] **Create Azure SQL Database**
  - Free tier: 32GB per subscription
  - Run migrations: `dotnet ef database update`
  - Store connection string in Function App settings

- [ ] **Deploy Azure Function App** (Consumption Plan)
  - Publish `YSS.Functions` project
  - Configure App Settings:
    - `DefaultConnection` → Azure SQL connection string
    - `Modular11__TournamentId`, `Modular11__*` → API settings
  - Test all 5 endpoints

- [ ] **Deploy Azure Static Web Apps** (free tier)
  - Connect GitHub repo
  - Configure build: `npm install → npm run build`
  - Set environment: `VITE_API_BASE_URL=https://<function-app-url>/api`
  - Configure routing (SPA: all routes → index.html)
  - Test filters with live data

### Phase 3 Extensions (Stretch Goals)

- [ ] **Match card layout consistency** — Clamp team names to single line with ellipsis
- [ ] **Mobile filter optimization** — Reduce vertical space on small screens (< 600px)
- [ ] **Standings page** — Win/loss records per program, season, region, age group
- [ ] **Google Maps upgrade** — Use Geocoding API for precise venue pins (vs general search)

---

## 🔑 Important Configuration

### Local Settings

**`YSS.Functions/local.settings.json`:**
```json
{
  "Host": {
    "CORS": "http://localhost:5173"  // Required for local dev
  }
}
```

**Why the `--functions` list in `func start`:**
- Timer trigger `ScheduledIngestion` requires Azurite (Azure Storage emulator) on port 10000
- For local dev, specify only HTTP triggers to avoid startup errors
- On Azure, timer trigger runs on schedule (midnight UTC, CRON: `0 0 0 * * *`)

### Database Connection

**LocalDB (Development):**
- Connection string: `Server=(localdb)\\mssqlLocalDb;Database=YSS;Trusted_Connection=true;`
- Start LocalDB: `sqllocaldb start mssqlLocalDb`
- Stop LocalDB: `sqllocaldb stop mssqlLocalDb`

**Migrations Location:**
- `MLSNext.Data/Migrations/` — EF Core migration files
- Current migrations:
  - `20260226183429_InitialCreate`
  - `20260227190439_RefactorDivisionToRegionHierarchy`
  - `20260227213955_IncreaseScoreColumnSize`
  - `20260228234737_AddTeamLogoUrl`
  - `20260302000000_SeedInitialLeagues`

---

## 📁 Project Structure

```
MLSNextSchedule/
├── MLSNext.Data/                 # EF Core + Entities
│   ├── Entities/                 # League, Division, Region, Match, Team, etc.
│   ├── AppDbContext.cs
│   ├── Migrations/
│   └── MLSNext.Data.csproj
├── MLSNext.Ingestion/            # Business Logic
│   ├── Services/                 # Modular11Client, ScheduleParser, MatchUpsertService, IngestionOrchestrator
│   ├── Models/                   # ParsedMatch (DTO)
│   └── MLSNext.Ingestion.csproj
├── MLSNext.Functions/            # Azure Functions Host
│   ├── Triggers/                 # GetMatches, GetTeams, GetDivisions, GetRegions, GetAgeGroups, ScheduledIngestion, TriggerIngestion
│   ├── Program.cs                # Dependency Injection
│   └── MLSNext.Functions.csproj
├── MLSNext.Tests/                # Unit + Integration Tests (36/36 passing)
│   ├── Unit/ScheduleParserTests.cs
│   ├── Integration/MatchUpsertServiceIntegrationTests.cs
│   ├── Integration/IngestionOrchestratorIntegrationTests.cs
│   └── MLSNext.Tests.csproj
├── MLSNext.Verification/         # CLI Tool for Testing
│   ├── Program.cs                # Multi-tournament ingestor
│   └── MLSNext.Verification.csproj
├── MLSNext.Web/                  # React Frontend
│   ├── src/
│   │   ├── components/           # ProgramSelector, SeasonSelector, FilterBar, MatchList, MatchCard
│   │   ├── App.tsx               # State management, API calls
│   │   ├── types.ts              # TypeScript interfaces
│   │   └── App.css, index.css
│   ├── package.json
│   ├── vite.config.ts
│   ├── tsconfig.json
│   └── README.md
├── MLSNextSchedule.slnx          # Solution file
└── PROJECT_STATUS_V2.md          # This file
```

---

## 🔗 API Reference

### GET /api/matches

**Query Parameters:**
- `league` — Filter by league name (e.g., "MLS Next")
- `program` — "Homegrown" or "Academy" (repeatable, multi-select)
- `season` — "fall2025" or "spring2026" (repeatable, multi-select)
- `team` — Team name substring search
- `region` — Region name
- `ageGroup` — Age group (e.g., "U13")
- `division` — Division name
- `startDate`, `endDate` — Date filtering (format: YYYY-MM-DD)

**Response:** Top 100 matches sorted by date, each with nested Team, Venue, AgeGroup, Region, Competition

---

### GET /api/teams

**Query Parameters:**
- `league` — Filter by league
- `program` — Repeatable
- `season` — Repeatable
- `region` — Geographic region

**Response:** All teams appearing in filtered matches, sorted by name

---

### GET /api/divisions, /api/regions, /api/agegroups

**Query Parameters:** None (returns all)

**Response:** All reference entities

---

## 🧪 Testing

```powershell
# Run all tests
dotnet test MLSNext.Tests

# Run specific test class
dotnet test MLSNext.Tests --filter "ScheduleParserTests"

# Run with coverage
dotnet test MLSNext.Tests /p:CollectCoverage=true /p:CoverageFormat=lcov
```

**Current Status:**
- ✅ Unit tests: 8/8 (ScheduleParser)
- ✅ Integration tests: 13/13 (MatchUpsertService, IngestionOrchestrator)
- ✅ Total: 36/36 passing

---

## 🚨 Known Limitations

1. **Modular11 API** — HTML parsing only (undocumented API, response format may change)
2. **Venue geocoding** — Google Maps search is approximate (may land on wrong field)
3. **Rate limiting** — Polite 200ms throttle built in, not enforced by API
4. **Timer trigger** — Requires Azurite locally; skip with `--functions` list in `func start`
5. **Sample data cap** — `MLSNext.Verification` ingests max 25 matches per tournament (remove cap for production)

---

## 📞 Troubleshooting

**Frontend won't call API:**
- Check CORS in `MLSNext.Functions/local.settings.json` points to `http://localhost:5173`
- Verify function app is running on port 7071
- Check browser console for CORS errors

**LocalDB won't connect:**
- Verify LocalDB is running: `sqllocaldb info`
- Check connection string in `AppDbContextFactory.cs`
- Restart LocalDB: `sqllocaldb stop mssqlLocalDb && sqllocaldb start mssqlLocalDb`

**Timer trigger errors:**
- Don't use `func start` without `--functions` list (missing Azurite)
- Always specify HTTP function list for local dev

**Tests fail after schema changes:**
- Run migrations: `dotnet ef database update --project MLSNext.Data`
- Clear LocalDB and re-ingest: `cd MLSNext.Verification && dotnet run`

---

## 🎯 Success Criteria

**Phase 4 Complete When:**
- ✅ Azure SQL database created and migrations applied
- ✅ Function App deployed and all 5 endpoints responding
- ✅ Static Web Apps deployed with VITE_API_BASE_URL configured
- ✅ Smoke tests pass: filters work, matches display with logos, calendar export works
- ✅ Production data ingested (remove MaxMatchesPerTournament cap)
