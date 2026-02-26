using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using MLSNext.Ingestion.Models;

namespace MLSNext.Ingestion.Services;

/// <summary>
/// Parses HTML fragments from Modular11 to extract match data.
/// Targets only mobile markup (visible-xs class) to avoid duplication.
/// </summary>
public class ScheduleParser
{
    private readonly ILogger<ScheduleParser> _logger;
    private readonly IBrowsingContext _context;

    public ScheduleParser(ILogger<ScheduleParser> logger)
    {
        _logger = logger;
        _context = BrowsingContext.New(Configuration.Default);
    }

    /// <summary>
    /// Parse HTML response and extract all matches.
    /// </summary>
    public List<ParsedMatch> ParseMatches(string htmlContent)
    {
        var matches = new List<ParsedMatch>();

        try
        {
            var document = _context.OpenAsync(req => req.Content(htmlContent)).Result;

            // Target only mobile markup (visible-xs containers)
            var mobileBlocks = document.QuerySelectorAll(".visible-xs");

            _logger.LogInformation("Found {Count} mobile blocks in HTML", mobileBlocks.Length);

            foreach (var block in mobileBlocks)
            {
                try
                {
                    var match = ExtractMatchFromBlock(block);
                    if (match != null)
                    {
                        matches.Add(match);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing match block");
                    continue;
                }
            }

            _logger.LogInformation("Parsed {Count} matches from HTML", matches.Count);
            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing HTML content");
            return new List<ParsedMatch>();
        }
    }

    /// <summary>
    /// Extract a single match from a mobile block element.
    /// </summary>
    private ParsedMatch? ExtractMatchFromBlock(IElement block)
    {
        // Find the table within the block
        var table = block.QuerySelector("table");
        if (table == null)
            return null;

        var rows = table.QuerySelectorAll("tr");
        if (rows.Length == 0)
            return null;

        var matchData = new Dictionary<string, string>();

        // Parse table rows as label-value pairs
        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length >= 2)
            {
                var label = cells[0].TextContent.Trim();
                var value = cells[1].TextContent.Trim();
                matchData[label] = value;
            }
        }

        // Extract required fields
        if (!matchData.TryGetValue("Match ID", out var matchId) || string.IsNullOrEmpty(matchId))
            return null;

        matchId = matchId.Trim();

        if (!matchData.TryGetValue("Date", out var dateStr) || string.IsNullOrEmpty(dateStr))
            return null;

        if (!DateTime.TryParse(dateStr, out var matchDate))
            return null;

        var homeTeam = GetValue(matchData, "Home Team");
        var awayTeam = GetValue(matchData, "Away Team");
        var ageGroup = GetValue(matchData, "Age");
        var gender = GetValue(matchData, "Gender");
        var competition = GetValue(matchData, "Competition");
        var division = GetValue(matchData, "Division");
        var venue = GetValue(matchData, "Venue");
        var score = GetValue(matchData, "Score");

        // Validate required fields
        if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(awayTeam) || 
            string.IsNullOrEmpty(ageGroup) || string.IsNullOrEmpty(division))
        {
            _logger.LogWarning("Match {MatchId} missing required fields", matchId);
            return null;
        }

        return new ParsedMatch
        {
            MatchId = matchId,
            MatchDate = matchDate,
            HomeTeamName = homeTeam,
            AwayTeamName = awayTeam,
            AgeGroup = ageGroup,
            Gender = gender ?? "Unknown",
            Competition = competition ?? "Unknown",
            Division = division,
            VenueName = venue ?? "TBD",
            Score = score
        };
    }

    private string? GetValue(Dictionary<string, string> data, string key)
    {
        return data.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) 
            ? value.Trim() 
            : null;
    }
}
