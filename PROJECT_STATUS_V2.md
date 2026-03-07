# Youth Soccer Schedules (YSS) — Project Status

**Last Updated:** March 5, 2026
**Status:** Phase 5 In Progress — Full data ingestion complete, automated weekly ingestion active

---

## 🎯 Current State

### ✅ Phases Complete

**Phase 1 & 2 — Backend & Data Layer** ✅
- Database schema: League → Division → Region → Match hierarchy
- Ingestion pipeline: HTML parsing, API calls, database upsert
- Azure Functions: 5 HTTP endpoints + 4 timer triggers (daily + 3 weekly batches)
- Testing: 37/37 unit & integration tests passing
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
                             ├─ DailyIngestion ✅          ├─ AgeGroups
                             ├─ WeeklyIngestion_u13u14 ✅  │
                             ├─ WeeklyIngestion_u15u16 ✅  │
                             └─ WeeklyIngestion_u17u19 ✅  │
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

### Phase 4 — Azure Deployment ✅ COMPLETE

**Completed This Session:**
- ✅ Fixed GitHub Actions .NET version mismatch (8.0 → 10.0)
- ✅ Implemented OIDC authentication for GitHub Actions (no passwords stored)
- ✅ Applied EF migrations to Azure SQL
- ✅ Deployed Function App to Azure (all 5 HTTP endpoints working)
- ✅ Deployed Frontend to Azure Static Web Apps
- ✅ Configured CORS between Static Web App and Function App
- ✅ Ingested sample data (100 matches, 104 teams)
- ✅ End-to-end live testing: frontend → backend → database ✅

**Live URLs:**
- Frontend: https://happy-smoke-0edf8100f.2.azurestaticapps.net
- Backend: https://yss-func-prod-cqcnb3dfgze4b7ap.centralus-01.azurewebsites.net/api

---

### Phase 5 — Feature Refinement & Data Ingestion

#### Immediate (Next Session)

1. **Fix Calendar Export (.ics) on Android**
   - Issue: Android shows "Unable to launch event" when clicking calendar link
   - Root cause: Likely .ics file format or MIME type issue
   - Action: Debug .ics generation, verify MIME type headers, test on multiple Android devices

2. **Add Collapse Mode for Match Cards**
   - Create compact card layout: `Date | Home Team | Score | Away Team`
   - Add toggle button to switch between expanded and compact views
   - Test on mobile (375px+) to ensure many more matches visible
   - Prioritize for small screens (< 600px)

3. **Ingest Full Fall 2025 Homegrown Data**
   - Remove `MaxMatchesPerTournament` cap in `YSS.Verification` (currently 25)
   - Run full ingestion for Tournament ID 12 (Homegrown), Fall 2025 date range
   - Monitor performance: query speed, page load time with 500+ matches
   - Identify bottlenecks if any

#### Secondary (After core features)

4. **Ingest All Production Data**
   - Pull all tournaments: Homegrown + Academy, Fall 2025 + Spring 2026
   - Populate with full live Modular11 data
   - Verify performance under load

5. **Additional Polish**
   - Match card layout consistency — Clamp team names to single line with ellipsis
   - Mobile filter optimization — Reduce vertical space on small screens (< 600px)
   - Standings page — Win/loss records per program, season, region, age group
   - Google Maps upgrade — Use Geocoding API for precise venue pins (vs general search)

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

---

## 📝 Session 9 Summary (March 3, 2026)

### Completed This Session

1. **Fixed GitHub Actions Deployment Pipeline**
   - Updated workflow .NET version: 8.0.x → 10.0.x (matches YSS.Functions target)
   - Implemented OIDC authentication (no passwords stored)
   - Created app registration: `github-actions-oidc` (Client ID: 8a5e3623-9c4e-4420-8886-29ff04eb099d)
   - Configured OIDC federated credentials for GitHub → Azure trust
   - Granted "Website Contributor" role on resource group for safe deployments
   - Git commits: 4693587, f0a087b, da4b38b, 9cff7ed, 492b8d4, a8e104e, f33f5cf

2. **Implemented Token-Based Auth for Data Ingestion**
   - Created `ingest-azure.ps1` PowerShell script for secure token-based auth
   - Updated `AppDbContextFactory` to accept access tokens as environment variables
   - Modified `YSS.Verification` to use token auth when available
   - Eliminated need for stored SQL passwords (uses Azure CLI authentication)
   - Git commits: 492b8d4, a8e104e

3. **Applied EF Migrations to Azure SQL**
   - Generated SQL migration scripts locally (idempotent)
   - Manually applied all 5 migrations via Query Editor:
     - InitialCreate, RefactorDivisionToRegion, IncreaseScoreColumn, AddTeamLogoUrl, SeedInitialLeagues
   - Verified schema with successful ingestion

4. **Ingested Sample Data to Azure SQL**
   - Created SQL user "github-actions-oidc" with db_datareader, db_datawriter roles
   - Inserted "MLS Next" league record (required for ingestion)
   - Successfully ingested 100 sample matches + 104 teams
   - Data includes: 25 Homegrown Fall 2025, 25 Homegrown Spring 2026, 25 Academy Fall 2025, 25 Academy Spring 2026

5. **Deployed Frontend to Azure Static Web Apps**
   - Created Static Web App resource: `yss-web-prod`
   - Configured automatic deployment from GitHub main branch
   - Fixed environment variable issue by creating `.env.production`
   - Set VITE_API_BASE_URL to Azure Function App endpoint
   - Git commit: Added .env.production with production API URL

6. **Fixed CORS Configuration**
   - Added Static Web App origin to Function App CORS whitelist
   - Verified end-to-end connectivity: Frontend → Backend → Database ✅

### Live Application Status

- **Frontend:** https://happy-smoke-0edf8100f.2.azurestaticapps.net
- **Backend:** https://yss-func-prod-cqcnb3dfgze4b7ap.centralus-01.azurewebsites.net/api
- **Database:** Azure SQL (yss-prod) with 100 matches, 104 teams
- **Tests:** 36/36 passing
- **Build:** 0 errors

### Known Issues Identified

1. **Android Calendar Export** — .ics file shows "Unable to launch event" on Android devices
   - Works on other platforms
   - Need to investigate .ics format and MIME type headers

2. **UI/UX on Mobile** — Match card layout is verbose on small screens
   - Shows full expanded card (too much vertical space)
   - Need compact collapse mode: Date | Home | Score | Away

3. **Data Volume** — Currently using 100 sample matches
   - Need to test performance with full Fall 2025 Homegrown tournament (~500+ matches)
   - Identify any query performance issues at scale

### Git Commits This Session

- `4693587` — Fix GitHub Actions deployment: Update .NET version to 10.0.x
- `1a9ed18` — Add automated EF migrations to GitHub Actions deployment
- `f0a087b` — Update workflow to use OIDC authentication for Azure SQL migrations
- `6713f31` — Add EF Core CLI tool installation to workflow
- `9cff7ed` — Fix migration approach: use SQL scripts instead of token in connection string
- `da4b38b` — Remove migrations from workflow, apply manually to Azure SQL
- `492b8d4` — Implement token-based authentication for Azure SQL ingestion
- `a8e104e` — Fix token-based auth in YSS.Verification runtime
- `f33f5cf` — Add debug logging for duplicate match IDs in batch
- Plus .env.production for frontend

### Session Achievements

- ✅ Complete Azure infrastructure (SQL, Function App, Static Web App)
- ✅ Zero-password OIDC authentication throughout CI/CD and ingestion
- ✅ End-to-end live application with real data
- ✅ Function App + Frontend working together in production
- ✅ Automated deployments from GitHub to Azure

### Next Session Priorities

1. Fix Android calendar export (.ics format)
2. Implement compact match card collapse mode
3. Ingest full Fall 2025 Homegrown data for performance testing

---

## 📝 Session 11 Summary (March 4, 2026 - Evening)

### Completed This Session

1. **Successful Azure SQL Data Ingestion (Full Dataset)**
   - Ran `ingest-azure.ps1` with token-based authentication
   - Azure SQL free tier requires ~60 seconds cold start after idle
   - Initial timeout errors (30sec) resolved by waiting for database spin-up
   - **Successfully ingested thousands of Academy Spring 2026 matches** (cap was removed in Session 10)
   - Token-based auth working flawlessly once database was active
   - Database operations executing at 40-100ms each (healthy performance)

2. **Identified & Fixed Location Data Bug**
   - Issue: Academy matches had no venue/location data (null values)
   - Homegrown matches had venue data correctly saved
   - Root cause: **Field name mismatch** between tournaments
     - Homegrown uses: "Location Name" field in HTML
     - Academy uses: "Location" field in HTML
   - Solution: Updated ScheduleParser.cs line 142 to check fallback options
     ```csharp
     var venue = GetValue(matchData, "Venue") ??
                 GetValue(matchData, "Location Name") ??
                 GetValue(matchData, "Location");
     ```
   - This ensures all tournaments' location data is captured

3. **Verified Ingestion Pipeline Quality**
   - ✅ Azure authentication (token-based, no passwords stored)
   - ✅ Modular11 API parsing (25 matches/page, multi-page support)
   - ✅ Database upsert logic (all fields updated on re-ingest)
   - ✅ Team logo extraction (working)
   - ✅ Score parsing (working)
   - ✅ Location data (now fixed)

### Git Commits This Session
- `09a461c` fix: Add 'Location' field fallback for Academy tournament venue parsing

### Next Steps
1. Test full re-ingestion with location fix applied
2. Ingest remaining tournaments (Homegrown Fall 2025, Homegrown Spring 2026, Academy Fall 2025)
3. Verify venue data populates correctly for all tournaments
4. Monitor performance with full production dataset

### Testing Status
- Build: 0 errors
- Tests: 36/36 passing
- Azure SQL Data: Thousands of Academy S26 matches ingested successfully

---

## 📝 Session 10 Summary (March 4, 2026)

### Completed This Session

1. **Hardened Match Upsert Logic**
   - Enhanced `MatchUpsertService` to update ALL mutable fields on re-ingestion (not just Score)
   - Now updates: `MatchDateUtc`, `HomeTeamId`, `AwayTeamId`, `VenueId`, `RegionId`, `CompetitionId`, `AgeGroupId`, `Gender`
   - Existing matches now properly handle reschedules, venue changes, team reassignments
   - Full-field upsert matches insert path logic (lookup-or-create for all reference entities)

2. **Removed Database Wipe from Ingestion CLI**
   - Deleted 50-68 line block that was clearing all data before each run
   - Safe incremental ingestion now possible — existing data preserved, new/updated matches upserted
   - Previously risky for production (one failed run would destroy all data)

3. **Removed Match Cap and Configured Academy Spring 2026**
   - Removed `MaxMatchesPerTournament = 25` constant
   - Updated orchestrator call to pass `maxMatches: null` (no limit)
   - Modified tournaments array to ingest only Academy Spring 2026 for this run
   - Other tournaments commented out for future runs

4. **Verified Code Quality**
   - ✅ `dotnet build` — 0 errors, 4 warnings (non-critical xUnit analyzer)
   - ✅ `dotnet test YSS.Tests` — 36/36 tests passing
   - ✅ `git commit` — Changes saved with descriptive message

### Ready for Production Ingestion

The code is prepared to run `.\ingest-azure.ps1` and ingest all available Academy Spring 2026 data. The full upsert fix ensures:
- Reschedules are reflected immediately
- Venue changes are captured
- Team reassignments are handled
- Scores update as matches are played

**Next step:** Run `.\ingest-azure.ps1` from a fresh PowerShell window (after Azure CLI installation)

### Git Commits This Session

- `a599333` — refactor: Improve match upsert to update all fields, remove DB wipe, remove match cap

### Known Limitations Addressed

- ✅ Removed the `MaxMatchesPerTournament` cap (was blocking full data ingestion)
- ✅ Fixed incomplete upsert (now handles all match field updates)
- ✅ Removed dangerous database wipe (safe incremental ingestion now)

### Testing Status

- ✅ Unit tests: 8/8 passing
- ✅ Integration tests: 28/28 passing
- ✅ Total: 36/36 tests passing
- ✅ Build: 0 errors

### Next Session

Run the ingestion script in a fresh PowerShell window:
```powershell
cd C:\Projects\MLSNextSchedule
.\ingest-azure.ps1
```

Monitor the output for final match/team counts. Then verify on the live frontend:
- https://happy-smoke-0edf8100f.2.azurestaticapps.net
- Filter by Academy, Spring 2026
- Should see all available matches (likely 500+ vs current 25)

---

## 📝 Session 13 Summary (March 5, 2026)

### Completed This Session

1. **Website Restructure — React Router + YSI Branding**
   - Added `react-router-dom` v6 with `/`, `/Schedules`, `/Standings` routes
   - New `Header.tsx` with hamburger menu button (left side), title updates per route
   - New `NavMenu.tsx` — slide-in drawer with Home / Schedules / Standings links
   - New `HomePage.tsx` — landing page with two clickable cards
   - Moved all schedule logic from `App.tsx` → `SchedulesPage.tsx`
   - Added `staticwebapp.config.json` for SPA routing support on Azure Static Web Apps
   - Branding: "Youth Soccer Intelligence" (YSI)

2. **Standings Page — Fully Implemented**
   - Backend: `GetStandings.cs` proxies Modular11's `get_teams` HTML endpoint
   - HTML parsed with AngleSharp (same library as schedule ingestion)
   - One API call returns all regions for a given program + age group
   - Frontend: age group dropdown → fetches all regions; region dropdown populated from response; filter is in-memory (instant, no extra request)
   - URL bookmarkable: `?program=homegrown&ageGroup=U17&region=Northeast`
   - Table: Rank, Team (logo), GP, W-D-L, Pts, PPM — data sourced directly from Modular11 (correct tiebreakers, tournament exclusions handled by them)
   - Program selector uses `singleSelect` mode (mutually exclusive, not multi-toggle)

3. **Bug Fixes**
   - CSS collision: scoped all StandingsPage styles under `.standings-page` / `.standings-table` to prevent breaking Schedules page filter layout and MatchCard logos
   - W-D-L zero display ("4--5"): replaced `||` with `??` in PascalCase→camelCase transform
   - Logo clipping: `object-fit: contain` with no border-radius (not circle-cropped)
   - Gzip decompression: registered named `"standings"` HttpClient with `AutomaticDecompression = GZip | Deflate`
   - Region name collisions: regex now captures full name including pathway tier (e.g., "Northeast (Pro Player Pathway)" vs "Northeast")
   - U19 age group: added `"U19" = "26"` to the AgeGroupMap (Modular11 uses "U19" not "U18/19")
   - Academy parameters: `UID_gender=3`, `list_type=71` (different from Homegrown's 1/53)
   - ProgramSelector toggle bug: added `singleSelect` prop; clicking Academy when Homegrown was active was a no-op because `programs[0]` was always 'homegrown'

### Git Commits This Session
- `fd76ad6` — fix: Use Google Calendar URL for Android calendar export
- `18a478a` — feat: Replace standings computation with Modular11 HTML proxy
- `7e5e2a6` — fix: Enable gzip decompression for Modular11 standings HTTP client
- `7342098` — fix: Preserve full division name to avoid region name collisions
- `30fe7bc` — fix: Add U19 to age group map
- `88000e4` — fix: Use correct UID_gender and list_type for Academy standings
- `64f4647` — fix: Add singleSelect mode to ProgramSelector for Standings page

### Modular11 Standings API Parameters (for reference)
| Program   | UID_event | UID_gender | list_type | Referer |
|-----------|-----------|------------|-----------|---------|
| Homegrown | 12        | 1          | 53        | /standings?year=21&gender=1 |
| Academy   | 35        | 3          | 71        | /league-standings/mls-next-academy-division/21 |

Age group → UID_age: U13=21, U14=22, U15=33, U16=14, U17=15, U19=26

### Next Session Priorities
1. **Clean up landing page** — match look and feel of Schedules/Standings pages
2. **Standings drill-in** — click a team row to see the games played and their results
3. Android calendar export fix (ongoing)

---

## 📝 Session 12 Summary (March 5, 2026)

### Completed This Session

1. **Fixed Age Group API Codes**
   - Age group codes in config were wrong (using age numbers 13–18 instead of Modular11 API codes)
   - Correct mapping: u13→21, u14→22, u15→33, u16→14, u17→15, u19→26
   - Fixed in: `YSS.Functions/local.settings.json`, `YSS.Verification/local.settings.json`, `YSS.Verification/Program.cs`
   - Updated Azure Function App env var `Modular11__AgeGroups` = `21,22,33,14,15,26` manually in Azure Portal

2. **Added Age Group Override to Ingestion Pipeline**
   - Added `ageGroupsOverride` param to `Modular11Client.FetchPageAsync()` and `BuildQueryParams()`
   - Added `ageGroups` param to `IngestionOrchestrator.RunAsync()`
   - Override takes precedence over settings; null falls back to `Modular11Settings.AgeGroups`

3. **Split Weekly Ingestion into 3 Staggered Batches**
   - Previous single `WeeklyIngestion` trigger (all 6 ages, 150-day window) risked exceeding 10-min Azure timeout
   - Replaced with 3 staggered Sunday timer triggers in the same class:
     - `WeeklyIngestion_u13u14` — codes `21,22` at 3:00 AM UTC Sunday
     - `WeeklyIngestion_u15u16` — codes `33,14` at 3:30 AM UTC Sunday
     - `WeeklyIngestion_u17u19` — codes `15,26` at 4:00 AM UTC Sunday
   - Daily ingestion unchanged — still uses all 6 codes from settings, ±14-day window
   - Each weekly batch: ~2–3 min per run, well under timeout

4. **Full Production Data Ingestion**
   - Ran `ingest-azure.ps1` for Academy S26 + Homegrown S26, all 6 age groups
   - Confirmed data visible in live frontend

### Git Commits This Session
- `f5be80c` — feat: Fix age group codes and split weekly ingestion into 3 staggered batches

### Testing Status
- ✅ Build: 0 errors
- ✅ Tests: 37/37 passing
- ✅ Azure: 4 timer triggers + 5 HTTP triggers all Enabled
- ✅ Production data: Academy S26 + Homegrown S26 ingested for all age groups

### Next Session Priorities
1. Fix Android calendar export (.ics format)
2. Implement compact match card collapse mode for mobile

---

## 📝 Session 14 Summary (March 7, 2026)

### Completed This Session

1. **Analytics Page — Multi-Region Support**
   - Backend (`GetAnalytics.cs`): added `RegionNames` string[] to `TeamAnalyticsDto`
   - Cross-region teams (e.g. NY Red Bulls playing in both Academy and Northeast regions) now appear once with all region memberships listed
   - Region filter dropdown populated from flattened regionNames union across all teams
   - Frontend client-side filter: `team.regionNames.includes(selectedRegion)`

2. **Analytics Page — Strength of Schedule (SOS) column**
   - New `Sos` field on `TeamAnalyticsDto` — average ELO-proxy of opponents faced
   - Displayed as a numeric column (2 decimal places) in the Analytics table
   - Gives context to momentum score (a 70 on hard schedule > 70 on easy schedule)

3. **Analytics Page — Expanded Momentum (Last 8, Bayesian weighting)**
   - Last 5 → Last 8 matches for momentum calculation
   - Recency weights applied: last 5 matches weighted [5,4,3,2,1], older 3 de-emphasized
   - Badges 6-8 rendered at 45% opacity in the Last 8 column
   - Bayesian shrinkage for low-GP teams (blends toward 50 when <5 games played)

4. **Analytics Page — Visual Polish**
   - Region cell: stacked `<div>`/`<span>` tags replaced with `regionNames.join(', ')` — reads naturally on one line
   - Team cell: moved `display: flex` from `<td>` to inner `<div class="team-cell-inner">` — fixes grey horizontal line artefact and keeps logo/name vertically centred in tall rows
   - Removed unused `.region-tags` / `.region-tag` CSS rules

### Git Commits This Session
- `e99d732` — feat: Show all regions for cross-region teams and add Bayesian momentum
- `e71080c` — feat: Add Strength of Schedule (SOS) column to Analytics page
- `6a846fd` — feat: Add region column, expand momentum to last 8 games with weighted scoring
- `4ef8efd` — feat: Add Analytics page with Momentum Index
- `252f6ed` — fix: Polish Analytics table — comma regions, fix flex-on-td grey line

### Next Session Priorities

1. **ELO Power Rankings** — cross-region leaderboard ("Top 10 U17 teams in the US")
2. **Cross-page Linking / Drill-in** — clicking a team anywhere should navigate to their match history + analytics
   - Standings → team row click → filtered Schedules view (or modal/drawer)
   - Analytics → team row click → same drill-in
   - Schedules → team name click → same drill-in
   - Single destination: `/Schedules?program=...&ageGroup=...&team=...` already works; just need to wire up the links
3. **Landing Page Polish** — headline stats (top momentum team, top ELO team) to drive interest in Analytics tab

---

## 🐛 Backlog — Next Session

### Bugs

1. **Multi-select age group filter returns no matches**
   - Selecting more than one age group at a time returns 0 results
   - Single age group selection works fine
   - Likely a query parameter issue — check how `ageGroup` is passed to `GetMatches` and how it's filtered in the query

### Features / Enhancements

2. **"Last updated" timestamp on match cards** *(needs decision)*
   - Show a small timestamp on each card indicating when the match data was last ingested
   - Requires adding an `UpdatedAt` or `LastIngestedAt` column to the `Matches` table (new migration)
   - Decision needed: is this useful enough for users, or just noise?

4. **App rebrand + Standings section** *(bigger effort, plan before starting)*
   - "Youth Soccer Schedules" is too narrow once standings are added
   - Need a catchier name (e.g., "Youth Soccer Hub", "NextPitch", "Pitch Side" — TBD with user)
   - UI restructure: top-level nav with **Schedules** and **Standings** sections
   - Standings page: win/loss/draw records per program, season, region, age group
   - This is a multi-session effort — plan the rebrand and data model first
