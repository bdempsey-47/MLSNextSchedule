using MLSNext.Ingestion.Models;

namespace MLSNext.Tests.Fixtures;

/// <summary>
/// Provides reusable test data for unit and integration tests.
/// </summary>
public static class TestDataFixture
{
    public static ParsedMatch CreateSampleParsedMatch(string matchId = "m-12345")
    {
        return new ParsedMatch
        {
            MatchId = matchId,
            MatchDate = DateTime.UtcNow.AddDays(7),
            HomeTeamName = "Dragons FC",
            AwayTeamName = "Phoenix United",
            AgeGroup = "U13",
            Gender = "Male",
            Division = "Premier",
            Competition = "AD",
            VenueName = "Central Park",
            Score = "TBD"
        };
    }

    public static List<ParsedMatch> CreateMultipleParsedMatches(int count = 3)
    {
        var matches = new List<ParsedMatch>();
        for (int i = 0; i < count; i++)
        {
            matches.Add(CreateSampleParsedMatch($"m-{1000 + i}"));
        }
        return matches;
    }

    public static string CreateSampleHtmlResponse()
    {
        return @"
<html>
  <body>
    <div class='visible-xs'>
      <div class='match-row'>
        <span class='match-id'>m-12345</span>
        <span class='date'>03/15/2026 14:00</span>
        <span class='home-team'>Dragons FC</span>
        <span class='away-team'>Phoenix United</span>
        <span class='age-group'>U13</span>
        <span class='gender'>Male</span>
        <span class='division'>Premier</span>
        <span class='competition'>AD</span>
        <span class='venue'>Central Park</span>
        <span class='score'>TBD</span>
      </div>
    </div>
  </body>
</html>
";
    }

    public static string CreateEmptyPaginationResponse()
    {
        return @"
<html>
  <body>
    <p>No data available</p>
  </body>
</html>
";
    }
}
