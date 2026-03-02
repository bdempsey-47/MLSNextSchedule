namespace YSS.Data.Entities;

using System.Text.Json.Serialization;

public class AgeGroup
{
    public int Id { get; set; }
    public required string Name { get; set; } // U13, U14, etc.

    // Navigation
    [JsonIgnore]
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
