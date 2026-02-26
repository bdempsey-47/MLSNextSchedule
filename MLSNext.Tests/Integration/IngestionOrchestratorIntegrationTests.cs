using FluentAssertions;
using Moq;
using Xunit;
using MLSNext.Data;
using MLSNext.Ingestion.Models;
using MLSNext.Ingestion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MLSNext.Tests.Integration;

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

        _clientMock = new Mock<Modular11Client>(
            new HttpClient(),
            new Mock<ILogger<Modular11Client>>().Object,
            new Modular11Settings
            {
                TournamentId = "35",
                Gender = "1",
                Status = "scheduled",
                MatchType = "2",
                AgeGroups = new List<string> { "13", "15", "17" },
                StartDate = "",
                EndDate = ""
            });

        _parser = new ScheduleParser();
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
    <div>
      <div>m-001</div>
      <div>03/15/2026 14:00</div>
      <div>Dragons</div>
      <div>Phoenix</div>
      <div>U13</div>
      <div>Male</div>
      <div>Premier</div>
      <div>AD</div>
      <div>Park A</div>
      <div>TBD</div>
    </div>
  </div>
</html>";

        _clientMock
            .Setup(c => c.FetchPageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(htmlResponse)
            .Returns(Task.FromResult("<html>No data available</html>")); // Second call ends pagination

        _clientMock
            .SetupSequence(c => c.FetchPageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(htmlResponse) // First page
            .ReturnsAsync("<html>No data available</html>"); // Pagination end

        // Act
        await _orchestrator.RunAsync();

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
  </div>
</html>";

        var page2Html = @"
<html>
  <div class='visible-xs'>
    <div>
      <div>m-002</div>
      <div>03/16/2026 16:00</div>
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

        _clientMock
            .SetupSequence(c => c.FetchPageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page1Html)
            .ReturnsAsync(page2Html)
            .ReturnsAsync("<html>No data available</html>"); // End pagination

        // Act
        await _orchestrator.RunAsync();

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
    <div>
      <div>m-dup-001</div>
      <div>03/15/2026 14:00</div>
      <div>Dragons</div>
      <div>Phoenix</div>
      <div>U13</div>
      <div>Male</div>
      <div>Premier</div>
      <div>AD</div>
      <div>Park</div>
      <div>TBD</div>
    </div>
    <div>
      <div>m-dup-001</div>
      <div>03/15/2026 14:00</div>
      <div>Dragons</div>
      <div>Phoenix</div>
      <div>U13</div>
      <div>Male</div>
      <div>Premier</div>
      <div>AD</div>
      <div>Park</div>
      <div>TBD</div>
    </div>
  </div>
</html>";

        _clientMock
            .SetupSequence(c => c.FetchPageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(htmlWithDuplicate)
            .ReturnsAsync("<html>No data available</html>");

        // Act
        await _orchestrator.RunAsync();

        // Assert
        _dbContext.Matches.Should().HaveCount(1);
        _dbContext.Matches.First().MatchId.Should().Be("m-dup-001");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
