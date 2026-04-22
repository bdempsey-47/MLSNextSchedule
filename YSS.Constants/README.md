# YSS.Constants

This project contains all magic number constants used across the YSS (Youth Soccer Intelligence) solution.

## Purpose

Centralizes hardcoded values to:
- Improve code maintainability
- Reduce errors from duplicate magic numbers
- Provide clear documentation of system values
- Enable easy updates when values change

## Constants Classes

### `EloConstants`
ELO rating system configuration.

| Constant | Value | Description |
|----------|-------|-------------|
| `DefaultEloRating` | 1500 | Starting ELO for new teams |
| `KFactor` | 30 | ELO volatility factor (higher = more volatile) |
| `HomeFieldAdvantage` | 100 | ELO boost for home team in predictions |

### `TournamentConstants`
Modular11 tournament identifiers.

| Constant | Value | Description |
|----------|-------|-------------|
| `HomegrownTournamentId` | "12" | MLS Next Homegrown program |
| `AcademyTournamentId` | "35" | MLS Next Academy program |
| `NjCupQualifierTournamentId` | 84 | NJ Cup Qualifier |

### `AgeGroupConstants`
Modular11 API age group code mappings.

**Important:** These codes are NOT sequential. The Modular11 API uses non-intuitive codes.

| Code | Age Group | Constant |
|------|-----------|----------|
| 21 | U13 | `U13Code` |
| 22 | U14 | `U14Code` |
| 33 | U15 | `U15Code` |
| 14 | U16 | `U16Code` |
| 15 | U17 | `U17Code` |
| 26 | U19 | `U19Code` |

**Note:** U18 is not currently configured. To add it, you'll need to determine its Modular11 API code.

#### Usage in Configuration
In `local.settings.json` or `appsettings.json`:
```json
{
  "Modular11": {
    "AgeGroups": "21,22,33,14,15,26"
  }
}
```

This translates to: U13, U14, U15, U16, U17, U19

### `Modular11ApiConstants`
Modular11 API configuration values.

| Constant | Value | Description |
|----------|-------|-------------|
| `GenderMale` | "1" | Male gender code |
| `MatchType` | "2" | Standard match type |
| `StatusScheduled` | "scheduled" | Filter for scheduled matches |
| `StatusCompleted` | "completed" | Filter for completed matches |
| `MinThrottleMilliseconds` | 1000 | Minimum API throttle delay |
| `MaxThrottleMilliseconds` | 3000 | Maximum API throttle delay |

### `ProgramConstants`
Program identification codes.

| Constant | Value | Description |
|----------|-------|-------------|
| `Academy` | "AG" | MLS Next Academy |
| `Homegrown` | "HG" | MLS Next Homegrown |

### `ValidationConstants`
Validation and length constraints.

| Constant | Value | Description |
|----------|-------|-------------|
| `MinSearchQueryLength` | 2 | Minimum chars for search queries |
| `MaxScoreLength` | 20 | Max length for match scores |
| `MaxGenderLength` | 20 | Max length for gender field |
| `MaxMatchIdLength` | 50 | Max length for match IDs |

## Usage Example

```csharp
using YSS.Constants;

// ELO rating
var newTeam = new Team 
{ 
    EloRating = EloConstants.DefaultEloRating 
};

// Tournament filtering
if (tournamentId == int.Parse(TournamentConstants.AcademyTournamentId))
{
    // Handle Academy matches
}

// Validation
if (searchQuery.Length < ValidationConstants.MinSearchQueryLength)
{
    return BadRequest("Query too short");
}

// Age group codes
var ageGroupCodes = new[] 
{
    AgeGroupConstants.U13Code,
    AgeGroupConstants.U14Code,
    AgeGroupConstants.U15Code
};
```

## Adding New Constants

When adding new constants:

1. Choose the appropriate class or create a new one
2. Add XML documentation comments
3. Update this README with the new values
4. Update consuming code to use the constant
5. Build and test

## Project References

Add this project reference to use constants:

```xml
<ItemGroup>
  <ProjectReference Include="..\YSS.Constants\YSS.Constants.csproj" />
</ItemGroup>
```

Then add the using statement:

```csharp
using YSS.Constants;
```
