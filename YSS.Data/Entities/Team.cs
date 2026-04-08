namespace YSS.Data.Entities;

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

public class Team
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Program { get; set; }  // "AG" or "HG"
    public string? LogoUrl { get; set; }  // CloudFront CDN URL from Modular11
    public int EloRating { get; set; } = 1500;

    // Navigation
    [JsonIgnore]
    public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
    [JsonIgnore]
    public ICollection<Match> AwayMatches { get; set; } = new List<Match>();

    // ELO rank (non-persisted, computed at runtime)
    [NotMapped]
    public int? EloRank { get; set; }

    [NotMapped]
    public int? EloTotal { get; set; }
}
