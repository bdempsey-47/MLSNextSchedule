# Youth Soccer Schedules (YSS) — Project Status

**Last Updated:** March 2, 2026 (Evening)  
**Status:** Phase 3 Complete → Phase 4 In Progress (Azure Deployment)

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

### Phase 4 — Azure Deployment (Continuing)

#### Immediate (Tomorrow Session)

1. **Debug GitHub Actions Deployment Failure**
   - Review action logs: https://github.com/bdempsey-47/MLSNextSchedule/actions
   - Common issues: EF migration not applied, connection string format, publish profile format
   - Fix: Update workflow or Azure config as needed

2. **Apply EF Migrations to Azure SQL**
   - Option A (Manual via Query Editor):
     - Use SQL migration scripts from `YSS.Data/Migrations/`
     - Run in order: InitialCreate → RefactorDivisionToRegion → IncreaseScoreColumn → AddTeamLogoUrl → SeedInitialLeagues
   - Option B (Automated in deployment):
     - Add migration step to GitHub Actions workflow
     - Or manually run: `dotnet ef database update --project YSS.Data` with Azure connection string

3. **Deploy Azure Function App**
   - Fix workflow and re-trigger deployment
   - Verify endpoints responding: `https://yss-func-prod.azurewebsites.net/api/matches`, etc.
   - Test with production data

#### Secondary (After backend is working)

4. **Deploy Frontend to Azure Static Web Apps**
   - Create Static Web App resource
   - Connect GitHub repo
   - Configure build & environment:
     - Build: `npm run build`
     - Output: `dist/`
     - Environment: `VITE_API_BASE_URL=https://yss-func-prod.azurewebsites.net/api`
   - Test filters with live backend

5. **Ingest Production Data**
   - Run Modular11 full tournament pull (remove sample cap)
   - Verify all matches, teams, venues loaded
   - Test calendar export, team filters, venue maps

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
    "LocalHttpPort": 7071,
    "CORS": "http://localhost:5173"  // Required for local dev - already configured
  }
}
```

**Azure Settings (Function App):**
- Connection string: `DefaultConnection` → Azure SQL connection string with Managed Identity
- Timer trigger: Midnight UTC (CRON: `0 0 0 * * *`)

**Why the `--functions` list in `func start`:**
- Timer trigger `ScheduledIngestion` requires Azurite (Azure Storage emulator) on port 10000
- For local dev, specify only HTTP triggers to avoid startup errors
- On Azure, timer trigger runs on schedule automatically
- Command: `func start --functions GetMatches GetTeams GetDivisions GetRegions GetAgeGroups TriggerIngestion`

### Database Connection

**LocalDB (Development):**
- Connection string: `Server=(localdb)\\mssqlLocalDb;Database=YSS;Trusted_Connection=true;`
- Start LocalDB: `sqllocaldb start mssqlLocalDb`
- Stop LocalDB: `sqllocaldb stop mssqlLocalDb`

**Azure SQL (Production):**
- Server: `yss-sql-prod.database.windows.net`
- Database: `yss-prod`
- Authentication: Entra-only (Managed Identity)
- Connection string format: `Server=yss-sql-prod.database.windows.net;Database=yss-prod;Authentication=Active Directory Managed Identity;Connection Timeout=30;`

**Migrations Location:**
- `YSS.Data/Migrations/` — EF Core migration files
- Current migrations:
  - `20260226183429_InitialCreate` — Base schema
  - `20260227190439_RefactorDivisionToRegionHierarchy` — League→Division→Region→Match
  - `20260227213955_IncreaseScoreColumnSize` — Team score columns
  - `20260228234737_AddTeamLogoUrl` — Team logo URLs
  - `20260302000000_SeedInitialLeagues` — League lookup data

---

## 📁 Project Structure

```
MLSNextSchedule/
├── YSS.Data/                     # EF Core + Entities
│   ├── Entities/                 # League, Division, Region, Match, Team, Venue, etc.
│   ├── Migrations/               # 5 migrations + SeedInitialLeagues
│   ├── AppDbContext.cs
│   ├── AppDbContextFactory.cs
│   └── YSS.Data.csproj
├── YSS.Ingestion/                # Business Logic
│   ├── Services/                 # Modular11Client, ScheduleParser, MatchUpsertService, IngestionOrchestrator
│   ├── Models/                   # ParsedMatch (DTO)
│   └── YSS.Ingestion.csproj
├── YSS.Functions/                # Azure Functions Host
│   ├── Triggers/                 # GetMatches, GetTeams, GetDivisions, GetRegions, GetAgeGroups, ScheduledIngestion, TriggerIngestion
│   ├── Program.cs                # Dependency Injection
│   ├── host.json
│   ├── local.settings.json       # CORS: http://localhost:5173
│   └── YSS.Functions.csproj
├── YSS.Tests/                    # Unit + Integration Tests (36/36 passing)
│   ├── Unit/ScheduleParserTests.cs
│   ├── Unit/Modular11ClientTests.cs
│   ├── Integration/MatchUpsertServiceIntegrationTests.cs
│   ├── Integration/IngestionOrchestratorIntegrationTests.cs
│   ├── Integration/FunctionsIntegrationTests.cs
│   ├── Fixtures/TestDataFixture.cs
│   └── YSS.Tests.csproj
├── YSS.Verification/             # CLI Tool for Testing
│   ├── Program.cs                # Multi-tournament ingestor
│   └── YSS.Verification.csproj
├── YSS.Web/                      # React Frontend
│   ├── src/
│   │   ├── components/           # ProgramSelector, SeasonSelector, FilterBar, MatchList, MatchCard, LeagueSelector
│   │   ├── App.tsx               # State management, API calls
│   │   ├── types.ts              # TypeScript interfaces
│   │   ├── App.css, index.css
│   │   └── images/
│   ├── index.html
│   ├── package.json
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── tsconfig.app.json
│   └── README.md
├── .github/workflows/
│   └── deploy.yml                # GitHub Actions CI/CD (build, test, deploy)
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
dotnet test YSS.Tests

# Run specific test class
dotnet test YSS.Tests --filter "ScheduleParserTests"

# Run with coverage
dotnet test YSS.Tests /p:CollectCoverage=true /p:CoverageFormat=lcov
```

**Current Status:**
- ✅ Unit tests: 8/8 (ScheduleParserTests, Modular11ClientTests)
- ✅ Integration tests: 15/15 (MatchUpsertService, IngestionOrchestrator, FunctionsIntegrationTests)
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
- Check CORS in `YSS.Functions/local.settings.json` points to `http://localhost:5173`
- Verify function app is running on port 7071
- Check browser console for CORS errors
- Use `func start --functions GetMatches GetTeams GetDivisions GetRegions GetAgeGroups TriggerIngestion` (exclude timer trigger)

**LocalDB won't connect:**
- Verify LocalDB is running: `sqllocaldb info`
- Check connection string in `YSS.Data/AppDbContextFactory.cs`
- Restart LocalDB: `sqllocaldb stop mssqlLocalDb && sqllocaldb start mssqlLocalDb`

**Azure SQL connection fails:**
- Verify connection string in Function App settings (Configuration → Connection Strings)
- Check Managed Identity is enabled on Function App
- Verify database user exists: `SELECT name FROM sys.server_principals WHERE name = 'yss-func-prod'`
- Verify public endpoint is enabled on SQL server
- Check firewall rule: "Allow Azure services and resources" is ON

**Timer trigger errors:**
- Don't use `func start` without `--functions` list (missing Azurite)
- Always specify HTTP function list for local dev

**Tests fail after schema changes:**
- Run migrations: `dotnet ef database update --project YSS.Data`
- Clear LocalDB and re-ingest: `cd YSS.Verification && dotnet run`

**GitHub Actions deployment fails:**
- Check action logs: GitHub repo → Actions tab
- Common issues:
  - EF migrations not applied to Azure SQL
  - Connection string format incorrect
  - Publish profile malformed
  - Function triggers not enabled on deployment

---

## 🎯 Success Criteria

**Phase 4 Complete When:**
- ✅ Azure SQL database created and migrations applied
- ✅ Function App deployed and all 5 endpoints responding
- ✅ Static Web Apps deployed with VITE_API_BASE_URL configured
- ✅ Smoke tests pass: filters work, matches display with logos, calendar export works
- ✅ Production data ingested (remove MaxMatchesPerTournament cap)

---

## 📝 Session 8 Summary (March 2, 2026)

### Completed This Session

1. **Project Rebranding (Final Phase)**
   - Renamed all backend folders: MLSNext.* → YSS.*
   - Renamed .csproj files to match folder names
   - Updated solution file (MLSNextSchedule.slnx) with new paths
   - Verified build (0 errors) and tests (36/36 passing)
   - Git commit: ecc0e99 "Physical folder and project file renames"

2. **Azure Infrastructure Provisioning**
   - Created SQL Server: `yss-sql-prod` (Entra admin: current user)
   - Created Database: `yss-prod` (free tier, public endpoint, firewall rules)
   - Created Function App: `yss-func-prod` (Consumption, .NET 8, System-assigned MI)
   - Connected Function App and SQL Server in same region

3. **Security & Configuration**
   - Enabled Managed Identity on Function App (System-assigned)
   - Added connection string to Function App settings: `DefaultConnection`
   - Created database user for Managed Identity: `yss-func-prod`
   - Granted permissions: db_datareader, db_datawriter roles

4. **CI/CD Pipeline Setup**
   - Created GitHub Actions workflow: `.github/workflows/deploy.yml`
   - Workflow triggers: Push to main + manual dispatch
   - Pipeline: Build → Test (36/36) → Deploy to Azure
   - Added publish profile to GitHub Secrets: `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`
   - First deployment triggered but failed (TBD debugging next session)

### Known Issues / TBD

1. **Deployment Error** — "Deploy to Azure Functions" step failed
   - Likely causes: EF migrations not applied, connection string format, publish profile
   - Action: Review GitHub Actions logs and fix in next session

2. **Database Schema** — EF migrations not yet applied to Azure SQL
   - Action: Apply migrations manually via Query Editor or update workflow

3. **Frontend Deployment** — Not yet started
   - Blocked by backend deployment
   - Will deploy to Azure Static Web Apps once backend is working

### Testing Status

- ✅ Local development: Frontend & backend both running correctly
- ✅ API endpoints: All 6 HTTP functions responding (localhost:7071)
- ✅ Database: Schema ready (migrations pending Azure SQL)
- ✅ Test suite: 36/36 passing with YSS.Tests.dll assembly name

### Git Commits This Session

- `d79c2eb` — Merge and push GitHub Actions workflow
- `991ed9f` — Add GitHub Actions CI/CD workflow for Azure Functions deployment

### Recommendations for Next Session

1. **Priority 1:** Debug GitHub Actions deployment failure
   - Check action run logs
   - Verify EF migrations status on Azure SQL
   - Manually apply migrations if needed

2. **Priority 2:** Verify backend is accessible from Azure
   - Test endpoints: https://yss-func-prod.azurewebsites.net/api/matches, etc.
   - Check connection string, database user, Managed Identity

3. **Priority 3:** Ingest production data
   - Run YSS.Verification to pull live Modular11 data
   - Verify matches, teams, venues appear in Azure SQL

4. **Priority 4:** Deploy frontend to Azure Static Web Apps
   - Create Static Web App resource
   - Set VITE_API_BASE_URL environment variable
   - Connect GitHub repo and deploy

### Resources

- **Azure Portal:** https://portal.azure.com
- **GitHub Actions:** https://github.com/bdempsey-47/MLSNextSchedule/actions
- **Solution:** YSS.* namespaces, YSS.* folders, solution file updated
- **Local Testing:** `func start --functions ...` and `npm run dev` still work perfectly
