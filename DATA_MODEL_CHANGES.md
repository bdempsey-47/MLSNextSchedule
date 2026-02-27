# Data Model Changes for League/Division/Region Hierarchy

## Current Schema (Before)
```
Division (NorthEast, Southeast, Mountain, Frontier)
  └── Match
```

## New Schema (After)
```
League (MLSNext)
  └── Division (Homegrown, Academy)
      └── Region (NorthEast, Southeast, Mountain, Frontier)
          └── Match
```

## Entity Changes

### New: League
```csharp
public class League
{
    public int Id { get; set; }
    public required string Name { get; set; }  // "MLSNext" (allows future expansion)
    
    // Navigation
    public ICollection<Division> Divisions { get; set; } = new List<Division>();
}
```

### New: Division (Homegrown/Academy)
```csharp
public class Division
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public required string Name { get; set; }  // "Homegrown" or "Academy"
    public int TournamentId { get; set; }      // 12 (Homegrown) or 35 (Academy)
    
    // Navigation
    public League League { get; set; } = null!;
    public ICollection<Region> Regions { get; set; } = new List<Region>();
}
```

### Renamed: Division → Region
```csharp
public class Region
{
    public int Id { get; set; }
    public int DivisionId { get; set; }
    public required string Name { get; set; }  // "NorthEast", "Southeast", "Mountain", "Frontier"
    
    // Navigation
    public Division Division { get; set; } = null!;
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
```

### Updated: Match
```csharp
public class Match
{
    public int Id { get; set; }
    public required string MatchId { get; set; }
    public DateTime MatchDateUtc { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int AgeGroupId { get; set; }
    public int RegionId { get; set; }           // Changed from DivisionId
    public int CompetitionId { get; set; }
    public int VenueId { get; set; }
    public string? Score { get; set; }
    
    // Navigation
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public AgeGroup AgeGroup { get; set; } = null!;
    public Region Region { get; set; } = null!;  // Changed from Division
    public Competition Competition { get; set; } = null!;
    public Venue Venue { get; set; } = null!;
}
```

## API Client Updates

### Modular11Settings
Add `TournamentId` to settings to support both Academy and Homegrown:
```csharp
public class Modular11Settings
{
    public required string TournamentId { get; set; }  // "12" (Homegrown) or "35" (Academy)
    // ... rest of settings
}
```

### API Endpoints
- `GET /api/matches?division=academy&region=northeast&team=...&ageGroup=...`
- `GET /api/divisions` — Returns [Homegrown, Academy]
- `GET /api/regions?division=academy` — Returns regions for selected division
- `GET /api/teams?division=academy&region=northeast`

## Migration Strategy

1. Create new League table with single "MLSNext" record
2. Create new Division table with Homegrown (12) and Academy (35)
3. Create Region table from existing Division data
4. Add RegionId to Match, populate from current DivisionId
5. Drop old DivisionId foreign key
6. Optionally drop old Division table

## Modular11 Tournament IDs Discovered
- **Academy:** tournament=35
- **Homegrown:** tournament=12
