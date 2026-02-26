namespace MLSNext.Data.Entities;

public class AgeGroup
{
    public int Id { get; set; }
    public required string Name { get; set; } // e.g. "U15", "U17"

    // Navigation
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
