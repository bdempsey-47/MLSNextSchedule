# MLSNextSchedule

A full-stack ASP.NET Core application for ingesting, managing, and serving MLS Next match schedules. Built with Azure Functions, Entity Framework Core, and React.

**Status:** Phase 2 Complete — Functions Host Ready for Testing & Deployment

---

## 📋 Overview

**MLSNextSchedule** ingests match data from the Modular11 API (via HTML parsing), stores it in a SQL Server database, and exposes it through RESTful APIs. The system includes automated scheduling for periodic data synchronization and a responsive React frontend for browsing matches.

### Key Features

✅ **Automated Ingestion** — Parses Modular11 website for match data (every 4 hours by timer trigger)  
✅ **Full-Featured API** — Filter matches by team, date, age group, division  
✅ **Reference Management** — Automatic lookup-or-create pattern for teams, venues, divisions  
✅ **Duplicate Prevention** — In-memory deduplication across paginated API responses  
✅ **Comprehensive Testing** — Unit + integration tests with mocked HTTP and in-memory databases  
✅ **CI/CD Ready** — GitHub Actions automatically runs tests on push/PR  
✅ **Azure-First Design** — Functions, SQL Database, Static Web Apps deployment ready

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                  Azure Functions (HTTPTriggers + TimerTrigger)  │
│  GET /api/matches   GET /api/teams   GET /api/divisions        │
│  POST /api/ingestion/trigger         Every 4 hours scheduling  │
└──────────────────────┬──────────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────────┐
│              Ingestion Services (Orchestration)                  │
│  ├─ Modular11Client (HTTP pagination)                          │
│  ├─ ScheduleParser (HTML extraction via AngleSharp)            │
│  ├─ MatchUpsertService (lookup-or-create pattern)              │
│  └─ IngestionOrchestrator (full pipeline coordination)        │
└──────────────────────┬──────────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────────┐
│          Data Access Layer (EF Core + SQL Server)               │
│  Entities: Match, Team, Venue, Division, AgeGroup, Competition │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                  SQL Server DB
```

**Three-Tier Layering:**
- **MLSNext.Data** → EF Core entities, migrations, DbContext
- **MLSNext.Ingestion** → HTML parsing, API client, orchestration
- **MLSNext.Functions** → Azure Functions HTTP + Timer triggers
- **MLSNext.Tests** → Automated unit & integration tests

---

## 🛠️ Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Runtime** | .NET | 10.0 |
| **Database** | SQL Server / EF Core | 8.0.x |
| **HTML Parser** | AngleSharp | 1.x |
| **Functions Host** | Azure Functions Worker | 2.51.0 |
| **Testing** | xUnit + Moq + FluentAssertions | Latest |
| **Frontend** | React + TypeScript + Vite | (Phase 3) |

---

## 📦 Project Structure

```
MLSNextSchedule/
├── MLSNext.Data/                      # Database layer
│   ├── AppDbContext.cs
│   ├── Entities/                      # Match, Team, Venue, etc.
│   └── Migrations/                    # EF Core migrations
│
├── MLSNext.Ingestion/                 # Ingestion services
│   ├── Services/
│   │   ├── IngestionOrchestrator.cs   # Orchestrates full pipeline
│   │   ├── Modular11Client.cs         # HTTP client + pagination
│   │   ├── ScheduleParser.cs          # HTML extraction
│   │   └── MatchUpsertService.cs      # Database upsert logic
│   └── Models/
│       └── ParsedMatch.cs             # In-memory DTO
│
├── MLSNext.Functions/                 # Azure Functions host
│   ├── Program.cs                     # DI configuration
│   └── Triggers/
│       ├── GetMatches.cs              # GET /api/matches
│       ├── GetTeams.cs                # GET /api/teams
│       ├── GetDivisions.cs            # GET /api/divisions
│       ├── GetAgeGroups.cs            # GET /api/agegroups
│       ├── TriggerIngestion.cs        # POST /api/ingestion/trigger
│       └── ScheduledIngestion.cs      # Timer: every 4 hours
│
├── MLSNext.Tests/                     # Automated tests
│   ├── Unit/
│   │   ├── Modular11ClientTests.cs
│   │   └── ScheduleParserTests.cs
│   ├── Integration/
│   │   ├── MatchUpsertServiceIntegrationTests.cs
│   │   └── IngestionOrchestratorIntegrationTests.cs
│   └── Fixtures/
│       └── TestDataFixture.cs
│
├── .github/workflows/
│   └── build-and-test.yml             # CI/CD pipeline
│
├── ARCHITECTURE.md                    # Detailed file-by-file reference
├── TESTING.md                         # Testing guide & best practices
├── PROJECT_STATUS.md                  # Current phase status
└── README.md                          # This file
```

---

## 🚀 Quick Start

### Prerequisites

- **.NET 10.0 SDK** — [Download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- **SQL Server (Express or Local)** — For local development
- **Azure Functions Core Tools** — For local testing (optional)
- **Git** — For version control

### Local Development

```powershell
# Clone the repo
git clone https://github.com/YOUR_ORG/MLSNextSchedule.git
cd MLSNextSchedule

# Restore & build
dotnet restore
dotnet build

# Run tests
dotnet test MLSNext.Tests/

# Update local database (run migrations)
dotnet ef database update --project MLSNext.Data

# Start Azure Functions locally
cd MLSNext.Functions
func start

# Functions will be available at:
# http://localhost:7071/api/teams
# http://localhost:7071/api/matches?team=Dragons
```

### Configuration

**Local Settings** (`MLSNext.Functions/local.settings.json`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(local);Database=MLSNext;Trusted_Connection=true;Encrypt=false;"
  },
  "Modular11": {
    "TournamentId": "35",
    "Gender": "1",
    "AgeGroups": "13,14,15,16,17,18",
    "Status": "scheduled",
    "MatchType": "2"
  }
}
```

---

## 🧪 Testing

Comprehensive automated test suite covering all layers:

```powershell
# Run all tests
dotnet test

# Run specific test class
dotnet test MLSNext.Tests -k "ScheduleParserTests"

# Watch mode (re-run on file changes)
dotnet watch test

# With code coverage
dotnet test /p:CollectCoverage=true
```

**Test Categories:**
- **Unit Tests** — ScheduleParser (HTML), Modular11Client (HTTP)
- **Integration Tests** — MatchUpsertService (DB), IngestionOrchestrator (pipeline)
- **CI/CD** — Automatic execution on push to `main`/`develop`

See [TESTING.md](TESTING.md) for detailed testing guide.

---

## 📚 Documentation

| Document | Purpose |
|----------|---------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Detailed file-by-file technical reference |
| [TESTING.md](TESTING.md) | Testing guide, patterns, and best practices |
| [PROJECT_STATUS.md](PROJECT_STATUS.md) | Current development phase & roadmap |
| [ChatGPT_TechnicalGuidelines.md](ChatGPT_TechnicalGuidelines.md) | Original API specifications |

---

## 🔄 Development Workflow

### Adding a New Feature

1. **Create a branch** from `develop`
   ```powershell
   git checkout -b feature/my-feature
   ```

2. **Write tests first** (TDD)
   ```powershell
   # Add test in MLSNext.Tests/
   # Run: dotnet test
   ```

3. **Implement feature** in appropriate layer
   - Service logic → `MLSNext.Ingestion/Services/`
   - Database changes → `MLSNext.Data/Entities/` + migration
   - API endpoints → `MLSNext.Functions/Triggers/`

4. **Run full suite**
   ```powershell
   dotnet build
   dotnet test
   ```

5. **Commit & push**
   ```powershell
   git add .
   git commit -m "feat: add match filtering by age group"
   git push origin feature/my-feature
   ```

6. **Create Pull Request** on GitHub
   - CI/CD runs automatically
   - Code review required before merge

### Commit Message Convention

```
feat: add match date range filtering
fix: correct timezone handling in MatchDateUtc
refactor: simplify orchestrator pagination logic
docs: update TESTING.md with examples
test: add edge case for HTML parser
```

---

## 📊 Current Phases

### ✅ Phase 1: Data Layer & Ingestion Services
- EF Core entities with proper relationships
- HTML parsing with AngleSharp
- Modular11 API client with pagination & throttling
- Lookup-or-create reference table pattern

### ✅ Phase 2: Azure Functions Host
- 6 HTTP/Timer triggers
- Dependency injection wired
- Local development ready with `local.settings.json`

### ✅ Phase 3: Automated Testing
- 19+ unit & integration tests
- GitHub Actions CI/CD pipeline
- Test utilities & fixtures

### 📋 Phase 4: React Frontend (Upcoming)
- Vite + React + TypeScript
- Filter UI (team, age group, division, date range)
- Match card list display
- Azure Static Web Apps deployment

### 📋 Phase 5: Azure Deployment (Upcoming)
- SQL Database provisioning
- Function App deployment
- Static Web Apps setup
- Environment configuration

---

## 🔐 Environment Variables

### Local Development (`local.settings.json`)

```json
{
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

### Azure Deployment

Set in Azure Function App → Configuration → Application Settings:
- `ConnectionStrings__DefaultConnection` → Azure SQL connection string
- `Modular11__*` → Tournament settings
- `APPLICATIONINSIGHTS_CONNECTION_STRING` → Application Insights key

---

## 🐛 Troubleshooting

### Issue: "Migration not applied" error
**Solution:** Run `dotnet ef database update --project MLSNext.Data`

### Issue: Functions won't start locally
**Solution:** Install Azure Functions Core Tools: `choco install azure-functions-core-tools-3`

### Issue: Tests failing with database errors
**Solution:** Tests use in-memory DB; ensure `Microsoft.EntityFrameworkCore.InMemory` is installed

### Issue: Modular11 API returning 403
**Solution:** Check tournament ID and gender parameters in `local.settings.json`

---

## 📈 Performance & Scalability

- **Ingestion:** 200ms throttle between API requests (polite client)
- **Pagination:** In-memory deduplication with HashSet<string>
- **Database:** Natural key on MatchId prevents duplicates
- **Azure Functions:** Consumption Plan scales automatically
- **Frontend:** Static Web Apps CDN caches React builds

---

## 🤝 Contributing

1. Fork the repository
2. Create feature branch: `git checkout -b feature/amazing-feature`
3. Write tests & implementation
4. Run full test suite: `dotnet test`
5. Commit: `git commit -m 'feat: amazing feature'`
6. Push: `git push origin feature/amazing-feature`
7. Open Pull Request

---

## 📄 License

This project is licensed under the MIT License — see LICENSE file for details.

---

## 📞 Support

For questions or issues:
- 📖 See [ARCHITECTURE.md](ARCHITECTURE.md) for technical details
- 🧪 See [TESTING.md](TESTING.md) for testing guidance
- 📊 See [PROJECT_STATUS.md](PROJECT_STATUS.md) for current progress
- 💬 Open a GitHub issue for bugs or feature requests

---

## 🎯 Roadmap

- [x] Data access layer with EF Core
- [x] HTML parsing & API client
- [x] Azure Functions HTTP & Timer triggers
- [x] Automated unit & integration tests
- [x] GitHub Actions CI/CD
- [ ] React frontend with filtering
- [ ] Azure deployment (SQL + Function App + Static Web Apps)
- [ ] API documentation (Swagger/OpenAPI)
- [ ] Performance monitoring & alerting
- [ ] Admin dashboard for manual ingestion control

---

**Last Updated:** February 26, 2026  
**Maintained By:** Your Team  
**Repository:** [GitHub Link]
