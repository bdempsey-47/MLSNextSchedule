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

            if (mobileBlocks.Length == 0)
            {
                _logger.LogWarning("No .visible-xs elements found. Dumping first 1000 chars of HTML: {Html}", 
                    htmlContent.Substring(0, Math.Min(1000, htmlContent.Length)));
            }

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
        // Find the hidden details block (mobile-block-match-info)
        var detailsBlock = block.QuerySelector(".mobile-block-match-info");
        if (detailsBlock == null)
        {
            _logger.LogDebug("No .mobile-block-match-info found in block");
            return null;
        }

        var matchData = new Dictionary<string, string>();

        // Get all divs that have "row-heading-mobile" or "row-content-mobile" classes
        var headingElements = detailsBlock.QuerySelectorAll("[class*='row-heading-mobile']");
        _logger.LogDebug("Found {HeadingCount} heading elements", headingElements.Length);

        // For each heading, find its sibling content element
        foreach (var heading in headingElements)
        {
            var label = heading.TextContent.Trim();
            if (string.IsNullOrEmpty(label))
                continue;

            // Find the next row-content-mobile element at similar level
            var parent = heading.Parent as IElement;
            var contentElement = parent?.QuerySelector("[class*='row-content-mobile']");
            
            if (contentElement != null)
            {
                var value = contentElement.TextContent.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    matchData[label] = value;
                    _logger.LogDebug("Parsed field: {Label} = {Value}", label, value);
                }
            }
        }

        // Extract required fields
        if (!matchData.TryGetValue("Match ID", out var matchId) || string.IsNullOrEmpty(matchId))
        {
            _logger.LogDebug("Match ID not found. Available keys: {Keys}", string.Join(", ", matchData.Keys));
            return null;
        }

        matchId = matchId.Trim();

        if (!matchData.TryGetValue("Date", out var dateStr) || string.IsNullOrEmpty(dateStr))
        {
            _logger.LogDebug("Date not found for match {MatchId}", matchId);
            return null;
        }

        if (!DateTime.TryParse(dateStr, out var matchDate))
        {
            _logger.LogDebug("Could not parse date '{DateStr}' for match {MatchId}", dateStr, matchId);
            return null;
        }

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
            _logger.LogDebug("Match {MatchId} missing required fields: Home={Home}, Away={Away}, Age={Age}, Div={Div}", 
                matchId, homeTeam, awayTeam, ageGroup, division);
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
