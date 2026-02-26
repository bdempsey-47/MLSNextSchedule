namespace MLSNext.Data.Entities;

public class Venue
{
    public int Id { get; set; }
    public required string Name { get; set; } // Field / stadium name

    // Navigation
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
