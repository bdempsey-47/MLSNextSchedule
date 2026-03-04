# Claude Code Instructions for YSS Project

## Project Overview
- **Name:** Youth Soccer Schedules (YSS)
- **Tech Stack:** React 18 + TypeScript (frontend), .NET 8 (backend), Azure SQL
- **Status:** Phase 4 complete (live on Azure), Phase 5 in progress
- **Live URLs:**
  - Frontend: https://happy-smoke-0edf8100f.2.azurestaticapps.net
  - Backend: https://yss-func-prod-cqcnb3dfgze4b7ap.centralus-01.azurewebsites.net/api
  - Database: Azure SQL (yss-prod)

## Key Conventions
- Folder structure: `YSS.*` (not MLSNext — migration completed)
- Frontend: React components in `YSS.Web/src/components/`
- Backend: Azure Functions in `YSS.Functions/Triggers/`
- Database: EF Core entities in `YSS.Data/Entities/`
- Ingestion: YSS.Verification tool with token-based auth
- Tests: 36/36 passing (run with `dotnet test YSS.Tests`)
- Solution file: `MLSNextSchedule.slnx` (name stays, internal structure is YSS.*)

## Authentication & Security (IMPORTANT)
- **OIDC for GitHub Actions:** No passwords stored, uses federated credentials
  - App Registration: `github-actions-oidc`
  - Client ID: `8a5e3623-9c4e-4420-8886-29ff04eb099d`
  - Tenant ID: `dc96ec0c-8477-49bd-ab55-1c177e18c70c`
- **Token-Based Auth for Ingestion:** Uses Azure CLI to get access tokens
  - Script: `ingest-azure.ps1` (PowerShell)
  - No SQL passwords stored anywhere
  - Environment variable: `AZURE_SQL_ACCESS_TOKEN`
- **Frontend Environment:** `.env.production` has `VITE_API_BASE_URL`
  - Do NOT store secrets in .env files
  - Use Azure Static Web App configuration for runtime env vars

## Important URLs & Commands

### Local Development
```powershell
# Terminal 1: Backend (HTTP functions only, no timer trigger)
cd YSS.Functions
func start --functions GetMatches GetTeams GetDivisions GetRegions GetAgeGroups TriggerIngestion
# Runs on http://localhost:7071

# Terminal 2: Frontend
cd YSS.Web
npm run dev
# Runs on http://localhost:5173

# Terminal 3: Data Ingestion (Azure SQL)
cd C:\Projects\MLSNextSchedule
az login
.\ingest-azure.ps1
# Gets token from Azure CLI, runs ingestion to Azure SQL
```

### Local Development (LocalDB)
```powershell
# Start LocalDB
sqllocaldb start mssqlLocalDb

# Run ingestion locally (uses local.settings.json connection)
cd YSS.Verification
dotnet run
```

### Build & Test
```powershell
dotnet build                    # Build entire solution
dotnet test YSS.Tests           # Run all 36 tests
dotnet ef migrations add <Name> --project YSS.Data  # New migration
dotnet ef database update       # Apply to LocalDB
```

## Azure Configuration
- **Resource Group:** `application-prod`
- **Region:** Central US
- **SQL Server:** `yss-sql-prod.database.windows.net`
- **Database:** `yss-prod` (free tier, Entra-only auth)
- **Function App:** `yss-func-prod` → `yss-func-prod-cqcnb3dfgze4b7ap.centralus-01.azurewebsites.net`
- **Static Web App:** `yss-web-prod` → `happy-smoke-0edf8100f.2.azurestaticapps.net`

### Azure SQL Connection Strings
```
# Design-time (EF migrations)
Server=(localdb)\MSSQLLocalDB;Database=YSS;Trusted_Connection=true;Encrypt=false;

# Azure SQL with Active Directory Integrated (interactive)
Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Persist Security Info=False;User ID=YOUR_USERNAME;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Integrated;

# For GitHub Actions (token-based, in code)
Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
[+ set connection.AccessToken in code]
```

### GitHub Secrets
- `AZURE_CLIENT_ID`: 8a5e3623-9c4e-4420-8886-29ff04eb099d
- `AZURE_TENANT_ID`: dc96ec0c-8477-49bd-ab55-1c177e18c70c
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`: Downloaded from Function App → Get publish profile

## Current Known Issues (Phase 5 Priorities)
1. **Android Calendar Export** — .ics file shows "Unable to launch event"
   - Works on other platforms
   - Check: .ics format, MIME type headers, file encoding
   - File: `YSS.Web/src/components/MatchCard.tsx` (calendar export logic)

2. **Mobile UI** — Match card layout too verbose on small screens
   - Need: Compact collapse mode (Date | Home | Score | Away)
   - Target: Screens < 600px
   - File: `YSS.Web/src/components/MatchCard.tsx`

3. **Data Volume** — Only 100 sample matches loaded
   - Need: Ingest full Fall 2025 Homegrown tournament (~500+ matches)
   - Action: Remove `MaxMatchesPerTournament` cap in `YSS.Verification/Program.cs`
   - Test: Query performance, page load time, UI responsiveness

## User Preferences
- **Security-first:** OIDC, token-based auth, no stored passwords
- **Learning-oriented:** Prefer guidance and explanation over auto-execution
- **Cross-platform:** Use Bash for shell commands (Windows bash, not PowerShell)
- **Documentation:** Keep PROJECT_STATUS_V2.md updated after each session
- **Code quality:** Avoid over-engineering; keep changes focused and minimal

## Git Workflow
- **Branching:** Work on main (single-person project, auto-deploy)
- **Commits:** Use descriptive messages, include co-author tag
  ```
  git commit -m "Fix: Update X

  Description of changes and why.

  Co-Authored-By: Claude Haiku 4.5 <noreply@anthropic.com>"
  ```
- **Deployments:** Push to main auto-triggers:
  - GitHub Actions: Build, test, deploy Function App
  - Azure Static Web Apps: Build, deploy frontend
- **Rollback:** Check Azure Portal or GitHub Actions logs if something breaks

## Project Structure
```
MLSNextSchedule/
├── YSS.Data/                          # EF Core + Entities
│   ├── Entities/                      # League, Division, Region, Match, Team, Venue, AgeGroup, Competition
│   ├── Migrations/                    # 5 migrations (all applied to Azure SQL)
│   ├── AppDbContext.cs
│   ├── AppDbContextFactory.cs         # Supports token-based auth
│   └── YSS.Data.csproj
├── YSS.Ingestion/                     # Business Logic
│   ├── Services/                      # Modular11Client, ScheduleParser, MatchUpsertService, IngestionOrchestrator
│   ├── Models/                        # ParsedMatch (DTO)
│   └── YSS.Ingestion.csproj
├── YSS.Functions/                     # Azure Functions Host
│   ├── Triggers/                      # GetMatches, GetTeams, GetDivisions, GetRegions, GetAgeGroups, TriggerIngestion, ScheduledIngestion
│   ├── Program.cs                     # Dependency Injection
│   ├── host.json
│   ├── local.settings.json            # LocalDB connection, CORS: http://localhost:5173
│   └── YSS.Functions.csproj           # .NET 10 target framework
├── YSS.Tests/                         # Unit + Integration Tests (36/36 passing)
│   ├── Unit/
│   ├── Integration/
│   ├── Fixtures/
│   └── YSS.Tests.csproj
├── YSS.Verification/                  # CLI Tool for Data Ingestion
│   ├── Program.cs                     # Multi-tournament ingester with token auth support
│   ├── local.settings.json            # LocalDB connection string
│   └── YSS.Verification.csproj
├── YSS.Web/                           # React Frontend
│   ├── src/
│   │   ├── components/                # ProgramSelector, SeasonSelector, FilterBar, MatchList, MatchCard, LeagueSelector
│   │   ├── App.tsx                    # Main state management, API calls
│   │   ├── types.ts                   # TypeScript interfaces
│   │   ├── App.css, index.css
│   │   └── images/
│   ├── .env.production                # VITE_API_BASE_URL for Azure
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── package.json
│   └── README.md
├── .github/workflows/
│   ├── deploy.yml                     # GitHub Actions: build, test, deploy with OIDC
│   └── azure-static-web-apps-*.yml   # Auto-generated by Azure
├── ingest-azure.ps1                   # PowerShell script for token-based ingestion
├── migrations.sql                     # Generated SQL script (for reference)
├── CLAUDE.md                          # This file
├── PROJECT_STATUS_V2.md               # Session summaries and status
├── MLSNextSchedule.slnx               # Solution file (build all projects)
└── README.md
```

## Tips for Future Sessions
- Check `PROJECT_STATUS_V2.md` first to see where we left off
- Review this file if anything seems unfamiliar
- Update both files after each session
- Keep Azure resource names consistent (yss-* prefix)
- Test locally first, then deploy to Azure
- Monitor GitHub Actions and Azure Portal for any deployment issues
- Use `az account show` to verify Azure login status

## Debugging Checklist
If something breaks:
1. Check GitHub Actions logs (deploy.yml)
2. Check Azure Portal → Function App → Logs (Application Insights)
3. Check browser console (frontend errors)
4. Verify Azure SQL connection: `SELECT @@VERSION`
5. Verify CORS: Check Function App → CORS whitelist
6. Verify environment variables: Frontend (.env.production), Static Web App (Configuration)
7. Run local tests: `dotnet test YSS.Tests`
8. Test API locally: `func start` + `npm run dev`
