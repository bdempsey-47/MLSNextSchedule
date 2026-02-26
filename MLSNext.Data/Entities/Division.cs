namespace MLSNext.Data.Entities;

public class Division
{
    public int Id { get; set; }
    public required string Name { get; set; } // e.g. "Premier", "Select"

    // Navigation
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
