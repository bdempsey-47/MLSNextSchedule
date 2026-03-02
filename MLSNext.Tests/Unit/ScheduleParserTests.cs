using FluentAssertions;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using YSS.Ingestion.Services;
using YSS.Tests.Fixtures;

namespace YSS.Tests.Unit;

public class ScheduleParserTests
{
    private readonly ScheduleParser _parser;

    public ScheduleParserTests()
    {
        var mockLogger = new Mock<ILogger<ScheduleParser>>();
        _parser = new ScheduleParser(mockLogger.Object);
    }

    [Fact]
    public void ParseMatches_WithValidHtml_ExtractsMatchData()
    {
        // Arrange - Actual HTML structure from Modular11 API
        var html = @"
<html>
  <div class='visible-xs'>
    <!-- Match row with score -->
    <div class='row match-row-mobile'>
      <div class='col-xs-5'></div>
      <div class='col-xs-2 container-score text-center'>
        <span class='score-match-table mobile-color'>2-1</span>
      </div>
      <div class='col-xs-5'></div>
    </div>
    <!-- Match details block -->
    <div class='mobile-block-match-info'>
      <div class='row marg-0 dspl-f'>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Home Team</div>
          <div class='row row-content-mobile'><p>Dragons FC</p></div>
        </div>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Away Team</div>
          <div class='row row-content-mobile'><p>Phoenix United</p></div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Match ID</div>
          <div class='row row-content-mobile'>m-12345</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Date</div>
          <div class='row row-content-mobile'>03/15/2026 2:00pm</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Age</div>
          <div class='row row-content-mobile'>U13</div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Gender</div>
          <div class='row row-content-mobile'>MALE</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Competition</div>
          <div class='row row-content-mobile'>AD</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Division</div>
          <div class='row row-content-mobile'>Premier</div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-12'>
          <div class='row row-heading-mobile'>Location Name</div>
          <div class='row row-content-mobile'>Central Park</div>
        </div>
      </div>
    </div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html, 12);

        // Assert
        matches.Should().HaveCount(1);
        matches[0].MatchId.Should().Be("m-12345");
        matches[0].HomeTeamName.Should().Be("Dragons FC");
        matches[0].AwayTeamName.Should().Be("Phoenix United");
        matches[0].AgeGroup.Should().Be("U13");
        matches[0].Gender.Should().Be("MALE");
        matches[0].Division.Should().Be("Premier");  // Parser extracts what's in HTML
        matches[0].TournamentId.Should().Be(12);     // Parser preserves tournament ID
        matches[0].Competition.Should().Be("AD");
        matches[0].VenueName.Should().Be("Central Park");
        matches[0].Score.Should().Be("2-1");
    }

    [Fact]
    public void ParseMatches_WithMultipleMatches_ReturnsAll()
    {
        // Arrange - Multiple matches in actual API structure
        var html = @"
<html>
  <div class='visible-xs'>
    <!-- Match row with score -->
    <div class='row match-row-mobile'>
      <div class='col-xs-2 container-score text-center'>
        <span class='score-match-table mobile-color'>1-0</span>
      </div>
    </div>
    <div class='mobile-block-match-info'>
      <div class='row marg-0 dspl-f'>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Home Team</div>
          <div class='row row-content-mobile'><p>Team A</p></div>
        </div>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Away Team</div>
          <div class='row row-content-mobile'><p>Team B</p></div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Match ID</div>
          <div class='row row-content-mobile'>m-001</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Date</div>
          <div class='row row-content-mobile'>03/15/2026 2:00pm</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Age</div>
          <div class='row row-content-mobile'>U13</div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Gender</div>
          <div class='row row-content-mobile'>MALE</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Competition</div>
          <div class='row row-content-mobile'>AD</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Division</div>
          <div class='row row-content-mobile'>Select</div>
        </div>
      </div>
    </div>
  </div>
  <div class='visible-xs'>
    <!-- Match row with score -->
    <div class='row match-row-mobile'>
      <div class='col-xs-2 container-score text-center'>
        <span class='score-match-table mobile-color'>3-2</span>
      </div>
    </div>
    <div class='mobile-block-match-info'>
      <div class='row marg-0 dspl-f'>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Home Team</div>
          <div class='row row-content-mobile'><p>Team C</p></div>
        </div>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Away Team</div>
          <div class='row row-content-mobile'><p>Team D</p></div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Match ID</div>
          <div class='row row-content-mobile'>m-002</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Date</div>
          <div class='row row-content-mobile'>03/15/2026 4:00pm</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Age</div>
          <div class='row row-content-mobile'>U15</div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Gender</div>
          <div class='row row-content-mobile'>FEMALE</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Competition</div>
          <div class='row row-content-mobile'>Premier</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Division</div>
          <div class='row row-content-mobile'>Champions</div>
        </div>
      </div>
    </div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html, 12);

        // Assert
        matches.Should().HaveCount(2);
        matches[0].MatchId.Should().Be("m-001");
        matches[0].HomeTeamName.Should().Be("Team A");
        matches[1].MatchId.Should().Be("m-002");
        matches[1].HomeTeamName.Should().Be("Team C");
    }

    [Fact]
    public void ParseMatches_WithEmptyHtml_ReturnsEmptyList()
    {
        // Arrange
        var html = "<html></html>";

        // Act
        var matches = _parser.ParseMatches(html, 12);

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
    <div class='row table-content-row'>
      <div>m-12345</div>
      <div>Dragons FC</div>
    </div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html, 12);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public void ParseMatches_WithMissingMobileBlockInfo_HandlesGracefully()
    {
        // Arrange - visible-xs without mobile-block-match-info container
        var html = @"
<html>
  <div class='visible-xs'>
    <div class='row match-row-mobile'>
      <!-- incomplete structure -->
    </div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html, 12);

        // Assert - Should skip blocks without proper structure
        matches.Should().BeEmpty();
    }

    [Theory]
    [InlineData("2-1", "2-1")]
    [InlineData("0-0", "0-0")]
    [InlineData("TBD", "TBD")]
    public void ParseMatches_WithVariousScores_ParsesCorrectly(string scoreInput, string expectedScore)
    {
        // Arrange - Score is extracted from the score-match-table span element
        var html = $@"
<html>
  <div class='visible-xs'>
    <!-- Match row with score -->
    <div class='row match-row-mobile'>
      <div class='col-xs-2 container-score text-center'>
        <span class='score-match-table mobile-color'>{scoreInput}</span>
      </div>
    </div>
    <div class='mobile-block-match-info'>
      <div class='row marg-0 dspl-f'>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Home Team</div>
          <div class='row row-content-mobile'><p>Dragons FC</p></div>
        </div>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Away Team</div>
          <div class='row row-content-mobile'><p>Phoenix United</p></div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Match ID</div>
          <div class='row row-content-mobile'>m-12345</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Date</div>
          <div class='row row-content-mobile'>03/15/2026 2:00pm</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Age</div>
          <div class='row row-content-mobile'>U13</div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Gender</div>
          <div class='row row-content-mobile'>MALE</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Competition</div>
          <div class='row row-content-mobile'>AD</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Division</div>
          <div class='row row-content-mobile'>Premier</div>
        </div>
      </div>
    </div>
  </div>
</html>";

        // Act
        var matches = _parser.ParseMatches(html, 12);

        // Assert
        matches.Should().HaveCount(1);
        matches[0].Score.Should().Be(expectedScore);
    }
}
