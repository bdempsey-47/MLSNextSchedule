namespace MLSNext.Data.Entities;

using System.Text.Json.Serialization;

public class Team
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // Navigation
    [JsonIgnore]
    public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
    [JsonIgnore]
    public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
}
