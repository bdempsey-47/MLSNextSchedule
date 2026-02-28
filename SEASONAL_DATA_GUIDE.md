# Seasonal Data Pull Guide

## Overview
This guide explains how to pull seasonal match data from Modular11 and use the new season filter in the web UI.

## What's Been Configured

### Backend API Changes
- **GetMatches endpoint** now supports a `season` parameter
- Supported seasons:
  - `fall2025` - July 1, 2025 to December 31, 2025
  - `spring2026` - January 1, 2026 to June 30, 2026
- Season filtering is applied automatically, or use explicit `startDate` and `endDate` parameters

### Frontend UI Changes
- New **SeasonSelector** component added above the filter bar
- Displays two toggle buttons: "Fall 2025" and "Spring 2026"
- Default season is "Fall 2025"
- Season selection automatically filters matches

## Pulling Data from Modular11

### Using the PowerShell Script
The easiest way to pull all seasonal data is to run the provided script:

```powershell
cd C:\Projects\MLSNextSchedule
.\pull_seasonal_data.ps1
```

This script will:
1. Pull Fall 2025 matches from Homegrown division (Tournament ID: 12)
2. Pull Fall 2025 matches from Academy division (Tournament ID: 35)
3. Pull Spring 2026 matches from Homegrown division (Tournament ID: 12)
4. Pull Spring 2026 matches from Academy division (Tournament ID: 35)

Each pull will process pagination automatically and insert/upsert matches into the database.

### Manual Data Pull (If Needed)
If you need to pull data manually, edit the `MLSNext.Verification/local.settings.json`:

```json
{
  "Modular11": {
    "TournamentId": "12",      // 12 = Homegrown, 35 = Academy
    "Gender": "1",
    "Status": "scheduled",
    "MatchType": "2",
    "AgeGroups": "13,14,15,16,17,18",
    "StartDate": "2025-07-01 00:00:01",
    "EndDate": "2025-12-31 23:59:59"
  }
}
```

Then run:
```powershell
cd C:\Projects\MLSNextSchedule\MLSNext.Verification
dotnet run
```

## API Usage Examples

### By Season
```
GET /api/matches?season=fall2025
GET /api/matches?season=spring2026
```

### By Season + Filter
```
GET /api/matches?season=fall2025&age=U17&team=Philadelphia
GET /api/matches?season=spring2026&division=Northeast
```

### By Explicit Date Range (Override Season)
```
GET /api/matches?startDate=2025-07-01&endDate=2025-09-30
```

## Tournament ID Mapping
- **Tournament 12** = Homegrown division (MLS Next's top competitive level)
- **Tournament 35** = Academy division (developmental program)

## Data Volume
The Modular11 API returns results paginated and includes all matches for the selected date range and division. The V8 pagination will continue until it receives a "No data available" response.

## Troubleshooting

### No matches appearing?
1. Check the date range is correct
2. Verify the TournamentId is 12 (Homegrown) or 35 (Academy)
3. Ensure age groups are specified (default: 13-18)
4. Check the database connection string is correct

### Script errors?
1. Ensure you have .NET installed
2. Verify the path to MLSNext.Verification is correct
3. Check that local.settings.json exists in MLSNext.Verification/

### API not responding?
1. Verify Azure Functions are running: `func start` in MLSNext.Functions directory
2. Check the API base URL is configured correctly in the frontend .env files
3. Review browser console for CORS or connection errors

## Season Selector on Web UI
The new season selector appears on the web application between the Program Selector and Filter Bar. It displays:
- Fall 2025 (July 1, 2025 - December 31, 2025)
- Spring 2026 (January 1, 2026 - June 30, 2026)

Clicking a season automatically filters matches to that date range.
