namespace YSS.Data.Entities;

public class Match
{
    // Identity: Natural key from source
    public string MatchId { get; set; } = null!; // Global unique ID from Modular11

    // Foreign keys
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int VenueId { get; set; }
    public int RegionId { get; set; }
    public int CompetitionId { get; set; }
    public int AgeGroupId { get; set; }

    // Match data
    public DateTime MatchDateUtc { get; set; }
    public string? Score { get; set; } // e.g. "2-1", or "TBD"
    public string? Gender { get; set; } // e.g. "Male", "Female"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public Venue Venue { get; set; } = null!;
    public Region Region { get; set; } = null!;
    public Competition Competition { get; set; } = null!;
    public AgeGroup AgeGroup { get; set; } = null!;
}
