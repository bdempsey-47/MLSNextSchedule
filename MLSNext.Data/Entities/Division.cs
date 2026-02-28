namespace MLSNext.Data.Entities;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a division within a league (e.g., Homegrown, Academy).
/// Maps to Modular11 tournament IDs.
/// </summary>
public class Division
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public required string Name { get; set; }  // "Homegrown" or "Academy"
    public int TournamentId { get; set; }      // 12 (Homegrown) or 35 (Academy)

    // Navigation
    [JsonIgnore]
    public League League { get; set; } = null!;
    [JsonIgnore]
    public ICollection<Region> Regions { get; set; } = new List<Region>();
}
