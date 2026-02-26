namespace MLSNext.Data.Entities;

public class Competition
{
    public int Id { get; set; }
    public required string Name { get; set; } // e.g. "AD" (Academy Division)

    // Navigation
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
