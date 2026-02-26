0## Plan: MLS NEXT Schedule Ingestion & Custom Web UI

**TL;DR:** A .NET 8 solution with two shared class libraries (data + ingestion logic) and an Azure Functions project as the host. HTTP Trigger functions serve the API; a Timer Trigger function runs the ingestion job on a schedule. Data lands in Azure SQL (free offer). A React (Vite) frontend is deployed to Azure Static Web Apps (free tier). Total hosting cost: $0.

---

**Steps**

**1. Solution & Project Structure**

Create `MLSNextSchedule.sln` with these projects:

- `MLSNext.Data` — class library: EF Core models, `AppDbContext`, migrations
- `MLSNext.Ingestion` — class library: `Modular11Client`, `ScheduleParser`, `IngestionOrchestrator`, `MatchUpsertService`
- `MLSNext.Functions` — Azure Functions project (.NET 8 isolated worker): all HTTP Triggers + the Timer Trigger
- `mlsnext-web/` — standalone Vite + React app (outside the .NET solution)

---

**2. Database Schema (Azure SQL — Free Offer)**

Define normalized EF Core entities in `MLSNext.Data`:

| Table | Key Columns |
|---|---|
| `Matches` | `MatchId` (PK, natural key), `MatchDateUtc`, `Score`, FK refs |
| `Teams` | `TeamId`, `Name` |
| `Venues` | `VenueId`, `Name` |
| `Divisions` | `DivisionId`, `Name` |
| `Competitions` | `CompetitionId`, `Name` |
| `AgeGroups` | `AgeGroupId`, `Name` (e.g. `U15`) |
| `RawIngestionLogs` | `Id`, `FetchedAt`, `PageNumber`, `RawHtml`, `ParsedMatchCount` |

`Matches` has a UNIQUE constraint on `MatchId` enforced at both EF and DB levels.

---

**3. Ingestion Logic (`MLSNext.Ingestion`)**

- `Modular11Client` — `HttpClient` wrapper for the Modular11 GET endpoint. Configurable `tournament`, `gender`, `age[]`, `status`, `match_type`, `start_date`, `end_date` from app settings. 200ms throttle between page requests.
- `ScheduleParser` — Uses **AngleSharp** to parse HTML fragments. Targets only `visible-xs` containers. Extracts the 10 required fields into `ParsedMatch` DTOs.
- `IngestionOrchestrator` — Pagination loop starting at `open_page = 1`. Stops on `"No data available"` string or empty match list. Maintains a per-run `HashSet<string>` of seen Match IDs.
- `MatchUpsertService` — Lookup-or-create for normalized tables (Teams, Venues, etc.), then upserts `Match` rows via EF Core.

This library has **no dependency on Azure Functions** — it's plain .NET logic, fully testable in isolation.

---

**4. Azure Functions Host (`MLSNext.Functions`)**

Uses the **.NET 8 isolated worker model**.

| Function | Trigger | Route / Schedule |
|---|---|---|
| `GetMatches` | HTTP GET | `/api/matches?team=&startDate=&endDate=&ageGroup=&division=` |
| `GetTeams` | HTTP GET | `/api/teams` |
| `GetDivisions` | HTTP GET | `/api/divisions` |
| `GetAgeGroups` | HTTP GET | `/api/agegroups` |
| `TriggerIngestion` | HTTP POST | `/api/ingestion/trigger` (manual test trigger) |
| `ScheduledIngestion` | Timer | `0 0 */4 * * *` (every 4 hours) |

Both `TriggerIngestion` and `ScheduledIngestion` call into `IngestionOrchestrator` — no duplicated logic. Configure CORS in `host.json` to allow the Static Web App origin.

---

**5. React Frontend (`mlsnext-web/` — Vite)**

- **Filter bar** — dropdowns for Team, Age Group, Division; date range picker
- **Match card list** — Home vs. Away, date/time (localized), venue, score/TBD; sorted ascending by date
- **Responsive layout** — mobile-first, targets 375px+ screens
- `VITE_API_BASE_URL` env var points to the Function App URL in production and `localhost:7071` in dev

---

**6. Azure Hosting — $0 Configuration**

| Resource | Azure Service | Tier | Cost |
|---|---|---|---|
| Functions (API + timer job) | Azure Function App | Consumption Plan | Free (1M exec/mo included) |
| Database | Azure SQL | Free Offer (32 GB) | Free |
| Frontend | Azure Static Web Apps | Free Tier | Free |
| Secrets / config | Function App Application Settings | — | Free |

> **Note:** The Azure SQL free offer is limited to **one free database per subscription**. If you already have one, the cheapest paid alternative is Azure SQL Basic (~$5/mo) or switching to **PostgreSQL Flexible Server** (also has a free tier option).

---

**7. Configuration & Secrets**

All settings live in Function App **Application Settings** (never in source):

- `Modular11__TournamentId`, `Modular11__Gender`, `Modular11__AgeGroups`, `Modular11__MatchType`
- `Modular11__StartDate`, `Modular11__EndDate` (or auto-calculated rolling window)
- `Ingestion__CronSchedule` (override the timer cron without redeploying)
- `ConnectionStrings__DefaultConnection`

Locally, these live in `local.settings.json` (git-ignored).

---

**Verification**

1. Run `func start` locally — call `POST /api/ingestion/trigger` to fire a manual run
2. Query `GET /api/matches` and confirm records appear with no duplicates
3. Run ingestion a second time — confirm row counts are unchanged (idempotency test)
4. Open the Vite dev server, apply filters, confirm correct match cards render
5. Deploy to Azure, smoke-test from a mobile device on the hosted URL

---

**Decisions**

- **Azure Functions Consumption Plan** replaces App Service to eliminate hosting cost; `BackgroundService` pattern dropped entirely
- **Timer Trigger** handles scheduling natively — no "always on" requirement, no idle-sleep problem
- **Isolated worker model** (.NET 8) chosen over the in-process model as it is the current Microsoft-recommended path
- **AngleSharp** for HTML parsing — cleaner CSS-class-based querying than HtmlAgilityPack for `visible-xs` targeting
- **Lookup-or-create** for normalized reference tables keeps schema clean without pre-population scripts
- **`MLSNext.Ingestion` is Functions-agnostic** — all core logic is unit-testable without the Azure runtime
