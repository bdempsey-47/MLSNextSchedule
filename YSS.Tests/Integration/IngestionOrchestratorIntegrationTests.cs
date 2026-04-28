using FluentAssertions;
using Moq;
using Xunit;
using YSS.Data;
using YSS.Ingestion.Models;
using YSS.Ingestion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace YSS.Tests.Integration;

public class IngestionOrchestratorIntegrationTests
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<Modular11Client> _clientMock;
    private readonly ScheduleParser _parser;
    private readonly MatchUpsertService _upsertService;
    private readonly IngestionOrchestrator _orchestrator;

    public IngestionOrchestratorIntegrationTests()
    {
        // Use in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"OrchestratorTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Seed initial league for tests
        var league = new YSS.Data.Entities.League { Name = "MLS Next" };
        _dbContext.Leagues.Add(league);
        _dbContext.SaveChanges();

        _clientMock = new Mock<Modular11Client>(
            new HttpClient(),
            new Mock<ILogger<Modular11Client>>().Object,
            new Modular11Settings
            {
                TournamentId = 35,
                Gender = "1",
                Status = "scheduled",
                MatchType = "2",
                AgeGroups = new List<string> { "13", "15", "17" },
                StartDate = "",
                EndDate = ""
            });

        _parser = new ScheduleParser(new Mock<ILogger<ScheduleParser>>().Object);
        var loggerMock = new Mock<ILogger<MatchUpsertService>>();
        _upsertService = new MatchUpsertService(_dbContext, loggerMock.Object);

        var orchestratorLoggerMock = new Mock<ILogger<IngestionOrchestrator>>();
        _orchestrator = new IngestionOrchestrator(_clientMock.Object, _parser, _upsertService, _dbContext, orchestratorLoggerMock.Object);
    }

    [Fact]
    public async Task RunAsync_WithSinglePage_IngestsAllMatches()
    {
        // Arrange
        var htmlResponse = @"
<html>
  <div class='visible-xs'>
    <div class='mobile-block-match-info'>
      <div class='row marg-0 dspl-f'>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Home Team</div>
          <div class='row row-content-mobile'><p>Dragons</p></div>
        </div>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Away Team</div>
          <div class='row row-content-mobile'><p>Phoenix</p></div>
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
          <div class='row row-content-mobile'>Premier</div>
        </div>
      </div>
    </div>
  </div>
</html>";

        _clientMock
            .SetupSequence(c => c.FetchPageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(htmlResponse) // First page
            .ReturnsAsync("<html>No data available</html>"); // Pagination end

        // Act
        await _orchestrator.RunAsync(CancellationToken.None, null, "MLS Next");

        // Assert
        _dbContext.Matches.Should().HaveCount(1);
        _dbContext.Matches.First().MatchId.Should().Be("m-001");
    }

    [Fact]
    public async Task RunAsync_WithMultiplePages_IngestsAllPagesUntilEnd()
    {
        // Arrange
        var page1Html = @"
<html>
  <div class='visible-xs'>
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
          <div class='row row-content-mobile'>Premier</div>
        </div>
      </div>
    </div>
  </div>
</html>";

        var page2Html = @"
<html>
  <div class='visible-xs'>
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
          <div class='row row-content-mobile'>03/16/2026 4:00pm</div>
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
          <div class='row row-content-mobile'>AD</div>
        </div>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Division</div>
          <div class='row row-content-mobile'>Select</div>
        </div>
      </div>
    </div>
  </div>
</html>";

        _clientMock
            .SetupSequence(c => c.FetchPageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page1Html)
            .ReturnsAsync(page2Html)
            .ReturnsAsync("<html>No data available</html>"); // End pagination

        // Act
        await _orchestrator.RunAsync(CancellationToken.None, null, "MLS Next");

        // Assert
        _dbContext.Matches.Should().HaveCountGreaterThanOrEqualTo(2);
        _dbContext.Matches.Should().Contain(m => m.MatchId == "m-001");
        _dbContext.Matches.Should().Contain(m => m.MatchId == "m-002");
    }

    [Fact]
    public async Task RunAsync_WithDuplicateMatches_DeduplicatesInMemory()
    {
        // Arrange
        var htmlWithDuplicate = @"
<html>
  <div class='visible-xs'>
    <div class='mobile-block-match-info'>
      <div class='row marg-0 dspl-f'>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Home Team</div>
          <div class='row row-content-mobile'><p>Dragons</p></div>
        </div>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Away Team</div>
          <div class='row row-content-mobile'><p>Phoenix</p></div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Match ID</div>
          <div class='row row-content-mobile'>m-dup-001</div>
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
  <div class='visible-xs'>
    <div class='mobile-block-match-info'>
      <div class='row marg-0 dspl-f'>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Home Team</div>
          <div class='row row-content-mobile'><p>Dragons</p></div>
        </div>
        <div class='col-xs-5'>
          <div class='row row-heading-mobile'>Away Team</div>
          <div class='row row-content-mobile'><p>Phoenix</p></div>
        </div>
      </div>
      <div class='row marg-0 pad-10'>
        <div class='col-xs-4'>
          <div class='row row-heading-mobile'>Match ID</div>
          <div class='row row-content-mobile'>m-dup-001</div>
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

        _clientMock
            .SetupSequence(c => c.FetchPageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(htmlWithDuplicate)
            .ReturnsAsync("<html>No data available</html>");

        // Act
        await _orchestrator.RunAsync(CancellationToken.None, null, "MLS Next");

        // Assert
        _dbContext.Matches.Should().HaveCount(1);
        _dbContext.Matches.First().MatchId.Should().Be("m-dup-001");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
