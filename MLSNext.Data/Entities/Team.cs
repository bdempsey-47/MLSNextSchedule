namespace MLSNext.Data.Entities;

public class Team
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // Navigation
    public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
    public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
}
