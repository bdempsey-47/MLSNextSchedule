namespace YSS.Data.Entities;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a soccer league (e.g., MLS Next).
/// Allows for future expansion to other leagues.
/// </summary>
public class League
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // Navigation
    [JsonIgnore]
    public ICollection<Division> Divisions { get; set; } = new List<Division>();
}
