using FluentAssertions;
using Moq;
using Xunit;
using MLSNext.Data;
using MLSNext.Ingestion.Models;
using MLSNext.Ingestion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MLSNext.Tests.Integration;

public class MatchUpsertServiceIntegrationTests
{
    private readonly AppDbContext _dbContext;
    private readonly MatchUpsertService _upsertService;

    public MatchUpsertServiceIntegrationTests()
    {
        // Use in-memory database for integration tests
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Seed initial league for tests
        var league = new MLSNext.Data.Entities.League { Name = "MLS Next" };
        _dbContext.Leagues.Add(league);
        _dbContext.SaveChanges();

        var loggerMock = new Mock<ILogger<MatchUpsertService>>();
        _upsertService = new MatchUpsertService(_dbContext, loggerMock.Object);
    }

    [Fact]
    public async Task UpsertMatchesAsync_WithNewMatch_InsertsSuccessfully()
    {
        // Arrange
        var match = new ParsedMatch
        {
            MatchId = "m-new-001",
            MatchDate = DateTime.UtcNow.AddDays(1),
            HomeTeamName = "New Dragons",
            AwayTeamName = "New Phoenix",
            AgeGroup = "U13",
            Gender = "Male",
            Division = "NorthEast",
            TournamentId = 12,
            Competition = "AD",
            VenueName = "New Park",
            Score = "TBD"
        };

        // Act
        await _upsertService.UpsertMatchesAsync(new List<ParsedMatch> { match });

        // Assert
        var dbMatch = await _dbContext.Matches.FirstOrDefaultAsync(m => m.MatchId == "m-new-001");
        dbMatch.Should().NotBeNull();
        dbMatch!.HomeTeam.Name.Should().Be("New Dragons");
        dbMatch.AwayTeam.Name.Should().Be("New Phoenix");
        dbMatch.Score.Should().Be("TBD");
    }

    [Fact]
    public async Task UpsertMatchesAsync_WithExistingMatch_UpdatesSuccessfully()
    {
        // Arrange
        var initialMatch = new ParsedMatch
        {
            MatchId = "m-existing-001",
            MatchDate = DateTime.UtcNow.AddDays(1),
            HomeTeamName = "Dragons",
            AwayTeamName = "Phoenix",
            AgeGroup = "U13",
            Gender = "Male",
            Division = "NorthEast",
            TournamentId = 12,
            Competition = "AD",
            VenueName = "Central Park",
            Score = "TBD"
        };

        await _upsertService.UpsertMatchesAsync(new List<ParsedMatch> { initialMatch }, "MLS Next");

        var updatedMatch = new ParsedMatch
        {
            MatchId = "m-existing-001",
            MatchDate = DateTime.UtcNow.AddDays(1),
            HomeTeamName = "Dragons",
            AwayTeamName = "Phoenix",
            AgeGroup = "U13",
            Gender = "Male",
            Division = "NorthEast",
            TournamentId = 12,
            Competition = "AD",
            VenueName = "Central Park",
            Score = "2-1" // Updated score
        };

        // Act
        await _upsertService.UpsertMatchesAsync(new List<ParsedMatch> { updatedMatch }, "MLS Next");

        // Assert
        var dbMatch = await _dbContext.Matches.FirstOrDefaultAsync(m => m.MatchId == "m-existing-001");
        dbMatch.Should().NotBeNull();
        dbMatch!.Score.Should().Be("2-1");
    }

    [Fact]
    public async Task UpsertMatchesAsync_WithMultipleMatches_InsertsAll()
    {
        // Arrange
        var matches = new List<ParsedMatch>
        {
            new ParsedMatch
            {
                MatchId = "m-bulk-001",
                MatchDate = DateTime.UtcNow.AddDays(1),
                HomeTeamName = "Team A",
                AwayTeamName = "Team B",
                AgeGroup = "U13",
                Gender = "Male",
                Division = "NorthEast",
                TournamentId = 12,
                Competition = "AD",
                VenueName = "Venue A",
                Score = "TBD"
            },
            new ParsedMatch
            {
                MatchId = "m-bulk-002",
                MatchDate = DateTime.UtcNow.AddDays(2),
                HomeTeamName = "Team C",
                AwayTeamName = "Team D",
                AgeGroup = "U15",
                Gender = "Female",
                Division = "SouthEast",
                TournamentId = 35,
                Competition = "AD",
                VenueName = "Venue B",
                Score = "TBD"
            }
        };

        // Act
        await _upsertService.UpsertMatchesAsync(matches, "MLS Next");

        // Assert
        var dbMatches = _dbContext.Matches.ToList();
        dbMatches.Should().HaveCountGreaterThanOrEqualTo(2);
        dbMatches.Should().Contain(m => m.MatchId == "m-bulk-001");
        dbMatches.Should().Contain(m => m.MatchId == "m-bulk-002");
    }

    [Fact]
    public async Task UpsertMatchesAsync_CreatesReferenceTables()
    {
        // Arrange
        var match = new ParsedMatch
        {
            MatchId = "m-ref-001",
            MatchDate = DateTime.UtcNow.AddDays(1),
            HomeTeamName = "Unique Team 1",
            AwayTeamName = "Unique Team 2",
            AgeGroup = "U17",
            Gender = "Male",
            Division = "Competitive",
            TournamentId = 12,
            Competition = "Premier League",
            VenueName = "Unique Venue",
            Score = "TBD"
        };

        // Act
        await _upsertService.UpsertMatchesAsync(new List<ParsedMatch> { match }, "MLS Next");

        // Assert
        _dbContext.Teams.Should().Contain(t => t.Name == "Unique Team 1");
        _dbContext.Teams.Should().Contain(t => t.Name == "Unique Team 2");
        _dbContext.AgeGroups.Should().Contain(a => a.Name == "U17");
        _dbContext.Divisions.Should().Contain(d => d.Name == "Homegrown");  // Now checking for division
        _dbContext.Regions.Should().Contain(r => r.Name == "Competitive"); // Changed to check region
        _dbContext.Venues.Should().Contain(v => v.Name == "Unique Venue");
    }

    [Fact]
    public async Task UpsertMatchesAsync_ReusesExistingReferenceRecords()
    {
        // Arrange - Insert first match
        var match1 = new ParsedMatch
        {
            MatchId = "m-reuse-001",
            MatchDate = DateTime.UtcNow.AddDays(1),
            HomeTeamName = "Reusable Team",
            AwayTeamName = "Other Team",
            AgeGroup = "U13",
            Gender = "Male",
            Division = "NorthEast",
            TournamentId = 12,
            Competition = "AD",
            VenueName = "Reusable Venue",
            Score = "TBD"
        };

        await _upsertService.UpsertMatchesAsync(new List<ParsedMatch> { match1 }, "MLS Next");
        var initialTeamCount = _dbContext.Teams.Count();

        // Act - Insert second match with same team and venue
        var match2 = new ParsedMatch
        {
            MatchId = "m-reuse-002",
            MatchDate = DateTime.UtcNow.AddDays(2),
            HomeTeamName = "Reusable Team", // Same team
            AwayTeamName = "Another Team",
            AgeGroup = "U13",
            Gender = "Male",
            Division = "NorthEast",
            TournamentId = 12,
            Competition = "AD",
            VenueName = "Reusable Venue", // Same venue
            Score = "TBD"
        };

        await _upsertService.UpsertMatchesAsync(new List<ParsedMatch> { match2 }, "MLS Next");

        // Assert - Team and venue should be reused, not duplicated
        _dbContext.Teams.Count().Should().Be(initialTeamCount + 1); // Only 1 new team added
        _dbContext.Venues.Count().Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
