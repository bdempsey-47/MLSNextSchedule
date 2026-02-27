namespace MLSNext.Data.Entities;

/// <summary>
/// Represents a geographic region within a division (e.g., NorthEast, Southeast, Mountain, Frontier).
/// Formerly named "Division" before the hierarchy was reorganized.
/// </summary>
public class Region
{
    public int Id { get; set; }
    public int DivisionId { get; set; }
    public required string Name { get; set; }

    // Navigation
    public Division Division { get; set; } = null!;
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
