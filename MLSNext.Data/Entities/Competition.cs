namespace MLSNext.Data.Entities;

using System.Text.Json.Serialization;

public class Competition
{
    public int Id { get; set; }
    public required string Name { get; set; } // AD, etc.

    // Navigation
    [JsonIgnore]
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
