# MLSNextSchedule — Testing Guide

**Last Updated:** February 26, 2026

---

## 📋 Overview

The project includes comprehensive automated tests across all layers:
- **Unit Tests** — Individual service/component testing with mocks
- **Integration Tests** — End-to-end workflows with real in-memory databases
- **Test Data Fixtures** — Reusable test data generators

**Test Framework:** xUnit 2.6.6  
**Mocking Library:** Moq 4.20.70  
**Assertion Library:** FluentAssertions 6.12.0

---

## 🏗️ Test Project Structure

```
MLSNext.Tests/
├── Unit/
│   ├── Modular11ClientTests.cs       # HTTP client query building, throttling
│   └── ScheduleParserTests.cs         # HTML parsing with various inputs
├── Integration/
│   ├── MatchUpsertServiceIntegrationTests.cs    # In-memory DB insert/update
│   └── IngestionOrchestratorIntegrationTests.cs # End-to-end ingestion flow
└── Fixtures/
    └── TestDataFixture.cs             # Reusable test data generators
```

---

## 🧪 Running Tests

### From Command Line

```powershell
# Run all tests
dotnet test

# Run specific test project
dotnet test MLSNext.Tests/MLSNext.Tests.csproj

# Run specific test class
dotnet test MLSNext.Tests/MLSNext.Tests.csproj -k "Modular11ClientTests"

# Run specific test method
dotnet test MLSNext.Tests/MLSNext.Tests.csproj -k "FetchPageAsync_WithValidPage_ReturnsHtmlContent"

# Run with detailed output
dotnet test --verbosity detailed

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=lcov
```

### From VS Code

Use the built-in tasks:
1. **Ctrl+Shift+B** → Run default build task
2. **Ctrl+Shift+P** → Search "Tasks: Run Task" → Select "test"
3. **Ctrl+Shift+P** → Select "test (watch)" for continuous testing during development

### Test Explorer Extension

Install **Test Explorer UI** for VS Code:
- View → Test Explorer
- Run/Debug tests directly from the sidebar
- Filter by name, status, or tag

---

## 📝 Test Categories

### Unit Tests: Modular11Client

**File:** `Unit/Modular11ClientTests.cs`

**Coverage:**
- ✅ Valid HTTP responses return content
- ✅ HTTP errors throw exceptions
- ✅ Correct page number parameter in query string
- ✅ Required query parameters (tournament, gender, status, etc.)

**Key Tests:**
```csharp
[Fact]
public async Task FetchPageAsync_WithValidPage_ReturnsHtmlContent()
{
    // Mock HTTP response
    // Assert content matches
}

[Theory]
[InlineData(1), InlineData(2), InlineData(10)]
public async Task FetchPageAsync_IncludesPageNumberInQuery(int pageNumber)
{
    // Verify page number in query
}
```

**Why Mocked:**
- No external Modular11 calls (fast, reliable)
- Test query parameter building deterministically
- Test timeout/error handling without network

---

### Unit Tests: ScheduleParser

**File:** `Unit/ScheduleParserTests.cs`

**Coverage:**
- ✅ Extracts match data from valid HTML
- ✅ Handles multiple matches in single response
- ✅ Returns empty list for no matches
- ✅ Targets only `visible-xs` CSS class (avoids desktop duplicates)
- ✅ Gracefully skips incomplete rows
- ✅ Parses various score formats (TBD, numerics, etc.)

**Key Tests:**
```csharp
[Fact]
public void ParseMatches_WithValidHtml_ExtractsMatchData()
{
    // Arrange HTML with one match
    // Act: Parse
    // Assert: All 10 fields extracted correctly
}

[Fact]
public void ParseMatches_WithoutVisibleXsClass_IgnoresDesktopMarkup()
{
    // Arrange HTML with desktop-only class
    // Act: Parse
    // Assert: Returns empty (correctly filters)
}

[Theory]
[InlineData("2-1"), InlineData("0-0"), InlineData("TBD")]
public void ParseMatches_WithVariousScores_ParsesCorrectly(string score)
{
    // Verify different score formats work
}
```

**Why Isolated:**
- No external HTML fetching
- Fast execution (milliseconds)
- Tests CSS selector logic thoroughly

---

### Integration Tests: MatchUpsertService

**File:** `Integration/MatchUpsertServiceIntegrationTests.cs`

**Coverage:**
- ✅ Inserts new matches successfully
- ✅ Updates existing matches (by MatchId)
- ✅ Handles multiple matches in batch
- ✅ Creates reference tables (Teams, Venues, etc.)
- ✅ Reuses existing reference records (no duplicates)

**Key Tests:**
```csharp
[Fact]
public async Task UpsertMatchesAsync_WithNewMatch_InsertsSuccessfully()
{
    // Create match, call upsert
    // Query in-memory DB
    // Assert: Record exists with correct data
}

[Fact]
public async Task UpsertMatchesAsync_ReusesExistingReferenceRecords()
{
    // Insert match 1 with Team A and Venue X
    // Get initial counts
    // Insert match 2 with same Team A and Venue X
    // Assert: Counts show no duplicates created
}
```

**Database:** In-memory SQLite
- No external SQL Server required
- Fast (milliseconds)
- Isolated per test (unique Guid-based DB name)

---

### Integration Tests: IngestionOrchestrator

**File:** `Integration/IngestionOrchestratorIntegrationTests.cs`

**Coverage:**
- ✅ Single-page pagination
- ✅ Multi-page pagination until "No data available"
- ✅ In-memory deduplication across pages
- ✅ Full end-to-end orchestration workflow

**Key Tests:**
```csharp
[Fact]
public async Task RunAsync_WithSinglePage_IngestsAllMatches()
{
    // Mock client to return 1 page, then end signal
    // Call orchestrator.RunAsync()
    // Assert: DB contains ingested matches
}

[Fact]
public async Task RunAsync_WithDuplicateMatches_DeduplicatesInMemory()
{
    // HTML contains same MatchId twice
    // Assert: Only 1 record in DB
}
```

**Mocking Strategy:**
- Mock Modular11Client to return predefined HTML
- Use real Parser, Upsert, DbContext
- Tests full pipeline behavior

---

## 🔄 Test Data Fixtures

**File:** `Fixtures/TestDataFixture.cs`

Reusable generators prevent duplicating test data:

```csharp
// Generate single test match
var match = TestDataFixture.CreateSampleParsedMatch("m-12345");

// Generate multiple matches
var matches = TestDataFixture.CreateMultipleParsedMatches(5);

// Generate sample HTML response
var html = TestDataFixture.CreateSampleHtmlResponse();

// Generate pagination-end response
var endHtml = TestDataFixture.CreateEmptyPaginationResponse();
```

---

## 🚀 Continuous Integration

### GitHub Actions Workflow

**File:** `.github/workflows/build-and-test.yml`

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop`

**Jobs:**
1. Checkout code
2. Setup .NET 10.0
3. Restore dependencies
4. Build (Release configuration)
5. Run tests (with xUnit logger)
6. Upload test results as artifact
7. Publish results in GitHub UI

**Badge (add to README.md):**
```markdown
![Build and Test](https://github.com/YOUR_ORG/MLSNextSchedule/workflows/Build%20and%20Test/badge.svg)
```

---

## 📊 Coverage Goals

| Layer | Target | Current |
|-------|--------|---------|
| Ingestion Services | 80%+ | In Progress |
| Data Layer (basic) | 60%+ | --  |
| Functions (HTTP) | 50%+ | Planned |

**To measure:**
```powershell
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
# Generates: coverage.opencover.xml
```

---

## 🔍 Best Practices

### ✅ DO

- **Name clearly:** `[MethodName]_[Condition]_[Expected]`  
  Example: `FetchPageAsync_WithValidPage_ReturnsHtmlContent`

- **Use Arrange-Act-Assert:**
  ```csharp
  // Arrange - setup
  // Act - execute
  // Assert - verify
  ```

- **One assertion per test** (preferably)  
  Use FluentAssertions for readability

- **Test edge cases:**
  - Empty inputs
  - Null values
  - Duplicate data
  - Maximum limits

- **Mock external dependencies:**
  - HTTP clients
  - Database (use in-memory for integration)
  - File I/O

- **Use Theories for multiple scenarios:**
  ```csharp
  [Theory]
  [InlineData(1)]
  [InlineData(10)]
  [InlineData(100)]
  public void TestWithMultipleValues(int input) { }
  ```

### ❌ DON'T

- Don't connect to real Modular11 API in tests
- Don't hard-code database connections
- Don't create external files (use in-memory alternatives)
- Don't use `Thread.Sleep()` for timing (use `CancellationToken`)
- Don't have interdependent tests (each must be independent)

---

## 🐛 Debugging Tests

### In VS Code

1. Open test file
2. Set breakpoint
3. Right-click test → "Debug Test"
4. Step through with F10/F11

### Via Command Line

```powershell
# Run single test with diagnostic output
dotnet test MLSNext.Tests/MLSNext.Tests.csproj -k "TestMethodName" --verbosity diagnostic
```

### Common Issues

**Issue:** "Platform not supported" error
- **Solution:** Use `net10.0` target framework (matching other projects)

**Issue:** Tests timeout
- **Solution:** Mock external calls; increase timeout in task settings

**Issue:** Database locked (in-memory)
- **Solution:** Each test gets unique DB name: `$"TestDb_{Guid.NewGuid()}"`

---

## 📈 Next Steps

### Short-term
1. ✅ Unit test Modular11Client (query building)
2. ✅ Unit test ScheduleParser (HTML extraction)
3. ✅ Integration test MatchUpsertService (DB operations)
4. ✅ Integration test IngestionOrchestrator (full pipeline)
5. Create unit tests for HTTP triggers (FakeHttpContext)
6. Add snapshot tests for HTML parsing edge cases

### Medium-term
1. Measure code coverage (target 70%+)
2. Add performance benchmarks (ingestion throughput)
3. Create API integration tests (test all endpoints)
4. Add database migration tests

### Long-term
1. Stress tests (10k matches at once)
2. Load tests (concurrent API calls)
3. E2E tests with real Azure resources (non-production)

---

## 📝 Test Checklist for New Features

Before submitting a PR:
- [ ] All tests pass locally (`dotnet test`)
- [ ] New code has unit tests (>80% coverage)
- [ ] Integration tests added for workflows
- [ ] No hardcoded test data (use fixtures)
- [ ] Test names are clear and descriptive
- [ ] Edge cases covered (null, empty, max boundary)
- [ ] Mocks used for external dependencies
- [ ] No flaky tests (randomness, timing)
