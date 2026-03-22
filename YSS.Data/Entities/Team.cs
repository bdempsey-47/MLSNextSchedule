namespace YSS.Data.Entities;

using System.Text.Json.Serialization;

public class Team
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? LogoUrl { get; set; }  // CloudFront CDN URL from Modular11
    public int EloRating { get; set; } = 1500;

    // Navigation
    [JsonIgnore]
    public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
    [JsonIgnore]
    public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
}
