namespace YSS.Data.Entities;

/// <summary>
/// Pre-computed snapshot of homepage statistics, updated nightly.
/// Allows GetHomepageStats endpoint to serve results quickly without
/// loading and processing thousands of matches in-memory per request.
/// </summary>
public class HomepageSnapshot
{
    public int Id { get; set; }

    // Serialized JSON of the complete HomepageStatsDto
    public string StatsJson { get; set; } = null!;

    // Metadata
    public DateTime ComputedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
