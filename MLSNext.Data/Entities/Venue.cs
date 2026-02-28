namespace MLSNext.Data.Entities;

using System.Text.Json.Serialization;

public class Venue
{
    public int Id { get; set; }
    public required string Name { get; set; } // Field / stadium name

    // Navigation
    [JsonIgnore]
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
