namespace YSS.Ingestion.Models;

/// <summary>
/// DTO representing a parsed match from the Modular11 API response HTML.
/// </summary>
public class ParsedMatch
{
    public required string MatchId { get; set; }
    public required DateTime MatchDate { get; set; }
    public required string HomeTeamName { get; set; }
    public required string AwayTeamName { get; set; }
    public string? HomeTeamLogoUrl { get; set; }
    public string? AwayTeamLogoUrl { get; set; }
    public required string AgeGroup { get; set; }
    public required string Gender { get; set; }
    public required string Competition { get; set; }
    public required string Division { get; set; }  // Geographic region: "NorthEast", "Southeast", etc.
    public required int TournamentId { get; set; } // 12 = Homegrown, 35 = Academy
    public required string VenueName { get; set; }
    public string? Score { get; set; }
}

