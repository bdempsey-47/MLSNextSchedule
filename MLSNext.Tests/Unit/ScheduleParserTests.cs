using FluentAssertions;
using Xunit;
using MLSNext.Ingestion.Services;
using MLSNext.Tests.Fixtures;

namespace MLSNext.Tests.Unit;

public class ScheduleParserTests
{
    private readonly ScheduleParser _parser = new();

    [Fact]
    public void ParseMatches_WithValidHtml_ExtractsMatchData()
    {
        // Arrange
        var html = @"
<html>
  <div class='visible-xs'>
    <div>
      <div>m-12345</div>
      <div>03/15/2026 14:00</div>
      <div>Dragons FC</div>
      <div>Phoenix United</div>
      <div>U13</div>
      <div>Male</div>
      <div>Premier</div>
      <div>AD</div>
      <div>Central Park</div>
      <div>TBD</div>
    </div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html);

        // Assert
        matches.Should().HaveCount(1);
        matches[0].MatchId.Should().Be("m-12345");
        matches[0].HomeTeamName.Should().Be("Dragons FC");
        matches[0].AwayTeamName.Should().Be("Phoenix United");
        matches[0].AgeGroup.Should().Be("U13");
        matches[0].Gender.Should().Be("Male");
        matches[0].Division.Should().Be("Premier");
        matches[0].Competition.Should().Be("AD");
        matches[0].Venue.Should().Be("Central Park");
        matches[0].Score.Should().Be("TBD");
    }

    [Fact]
    public void ParseMatches_WithMultipleMatches_ReturnsAll()
    {
        // Arrange
        var html = @"
<html>
  <div class='visible-xs'>
    <div>
      <div>m-001</div>
      <div>03/15/2026 14:00</div>
      <div>Team A</div>
      <div>Team B</div>
      <div>U13</div>
      <div>Male</div>
      <div>Premier</div>
      <div>AD</div>
      <div>Venue 1</div>
      <div>TBD</div>
    </div>
    <div>
      <div>m-002</div>
      <div>03/15/2026 16:00</div>
      <div>Team C</div>
      <div>Team D</div>
      <div>U15</div>
      <div>Female</div>
      <div>Select</div>
      <div>AD</div>
      <div>Venue 2</div>
      <div>2-1</div>
    </div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html);

        // Assert
        matches.Should().HaveCount(2);
        matches[0].MatchId.Should().Be("m-001");
        matches[1].MatchId.Should().Be("m-002");
    }

    [Fact]
    public void ParseMatches_WithEmptyHtml_ReturnsEmptyList()
    {
        // Arrange
        var html = "<html></html>";

        // Act
        var matches = _parser.ParseMatches(html);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public void ParseMatches_WithoutVisibleXsClass_IgnoresDesktopMarkup()
    {
        // Arrange - Desktop markup without visible-xs class
        var html = @"
<html>
  <div class='visible-lg'>
    <div>m-12345</div>
    <div>Dragons FC</div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public void ParseMatches_WithMissingFields_HandlesGracefully()
    {
        // Arrange - Incomplete row (less than 10 fields)
        var html = @"
<html>
  <div class='visible-xs'>
    <div>
      <div>m-12345</div>
      <div>03/15/2026 14:00</div>
      <div>Dragons FC</div>
    </div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html);

        // Assert - Should skip incomplete rows
        matches.Should().BeEmpty();
    }

    [Theory]
    [InlineData("2-1")]
    [InlineData("0-0")]
    [InlineData("TBD")]
    public void ParseMatches_WithVariousScores_ParsesCorrectly(string score)
    {
        // Arrange
        var html = $@"
<html>
  <div class='visible-xs'>
    <div>
      <div>m-12345</div>
      <div>03/15/2026 14:00</div>
      <div>Dragons FC</div>
      <div>Phoenix United</div>
      <div>U13</div>
      <div>Male</div>
      <div>Premier</div>
      <div>AD</div>
      <div>Central Park</div>
      <div>{score}</div>
    </div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html);

        // Assert
        matches.Should().HaveCount(1);
        matches[0].Score.Should().Be(score);
    }
}
