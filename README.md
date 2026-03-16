# YSI - Youth Soccer Intelligence

Full-stack application for ingesting, managing, and serving youth soccer match schedules, standings, and analytics. Parses match data from Modular11, stores in Azure SQL, and serves through Azure Functions + a React SPA.

**Status:** Phase 5 in progress | Live on Azure

- **Frontend:** https://happy-smoke-0edf8100f.2.azurestaticapps.net
- **API:** https://yss-func-prod-cqcnb3dfgze4b7ap.centralus-01.azurewebsites.net/api

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 18, TypeScript, Vite |
| Backend | .NET 10, Azure Functions (isolated worker) |
| Database | Azure SQL, EF Core 8 |
| HTML Parsing | AngleSharp |
| Hosting | Azure Static Web Apps (frontend), Azure Functions (API) |
| Auth | OIDC (GitHub Actions), Azure AD token-based (SQL) |
| CI/CD | GitHub Actions |
| Testing | xUnit, Moq, FluentAssertions |

---

## Architecture

```
┌──────────────────────────────────────────────────────┐
│                React SPA (Vite)                      │
│  Schedules | Standings | Analytics                   │
└─────────────────────┬────────────────────────────────┘
                      │
┌─────────────────────▼────────────────────────────────┐
│           Azure Functions (HTTP + Timer)              │
│  GetMatches  GetTeams  GetDivisions  GetRegions      │
│  GetAgeGroups  GetStandings  GetAnalytics            │
│  TriggerIngestion  DailyIngestion  WeeklyIngestion   │
└─────────────────────┬────────────────────────────────┘
                      │
┌─────────────────────▼────────────────────────────────┐
│           Ingestion Services (Orchestration)          │
│  Modular11Client → ScheduleParser → MatchUpsertService│
└─────────────────────┬────────────────────────────────┘
                      │
┌─────────────────────▼────────────────────────────────┐
│           Data Layer (EF Core + Azure SQL)            │
│  Match, Team, Venue, Division, Region, AgeGroup,     │
│  League, Competition                                 │
└──────────────────────────────────────────────────────┘
```

**Layering:**
- **YSS.Data** — EF Core entities, migrations, DbContext
- **YSS.Ingestion** — HTML parsing, Modular11 client, orchestration
- **YSS.Functions** — Azure Functions HTTP + Timer triggers
- **YSS.Web** — React SPA with routing, filters, standings, analytics
- **YSS.Verification** — CLI tool for manual data ingestion
- **YSS.Tests** — Unit + integration tests (37 passing)

---

## Project Structure

```
MLSNextSchedule/
├── YSS.Data/                  # EF Core + entities + migrations
├── YSS.Ingestion/             # Modular11Client, ScheduleParser, MatchUpsertService
├── YSS.Functions/             # Azure Functions triggers (HTTP + Timer)
│   └── Triggers/
│       ├── GetMatches.cs
│       ├── GetTeams.cs
│       ├── GetDivisions.cs
│       ├── GetRegions.cs
│       ├── GetAgeGroups.cs
│       ├── GetStandings.cs
│       ├── GetAnalytics.cs
│       ├── TriggerIngestion.cs
│       ├── DailyIngestion.cs
│       └── WeeklyIngestion.cs
├── YSS.Web/                   # React 18 + TypeScript + Vite
│   └── src/
│       ├── components/        # MatchCard, FilterBar, ProgramSelector, etc.
│       └── App.tsx
├── YSS.Tests/                 # xUnit tests (unit + integration)
├── YSS.Verification/          # CLI ingestion tool (token-based auth)
├── .github/workflows/         # CI/CD (build, test, deploy)
├── MLSNextSchedule.slnx       # Solution file
└── CLAUDE.md                  # Development instructions
```

---

## Quick Start

```bash
# Build & test
dotnet build
dotnet test YSS.Tests

# Backend (local)
cd YSS.Functions
func start --functions GetMatches GetTeams GetDivisions GetRegions GetAgeGroups TriggerIngestion GetAnalytics
# http://localhost:7071

# Frontend (local)
cd YSS.Web
npm run dev
# http://localhost:5173
```

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/matches` | Matches with filters (program, season, team, ageGroup, region) |
| GET | `/api/teams` | All teams |
| GET | `/api/divisions` | Divisions |
| GET | `/api/regions` | Regions |
| GET | `/api/agegroups` | Age groups |
| GET | `/api/standings` | Standings (program, season, region, ageGroup) |
| GET | `/api/analytics` | Momentum analytics (program, ageGroup, region) |
| POST | `/api/ingestion/trigger` | Manual ingestion trigger |
| Timer | `DailyIngestion` | Nightly data sync |
| Timer | `WeeklyIngestion` | Weekend batch ingestion (3 runs) |
