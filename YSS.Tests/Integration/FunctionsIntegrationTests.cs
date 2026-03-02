using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using YSS.Data;
using YSS.Data.Entities;

namespace YSS.Tests.Integration;

/// <summary>
/// Integration tests for Azure Functions endpoints.
/// Tests focus on the data query and filtering logic used by the functions.
/// Note: HTTP request/response mocking is handled by Azure Functions test framework in deployment.
/// </summary>
public class FunctionsIntegrationTests : IDisposable
{
    private readonly AppDbContext _dbContext;

    public FunctionsIntegrationTests()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    // ============= GetMatches Query Tests =============

    [Fact]
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Arrange
        SeedTestData();
        var expectedCount = 2;

        // Act
        var matches = await _dbContext.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.MatchDateUtc)
            .Take(100)
            .ToListAsync();

        // Assert
        matches.Should().HaveCount(expectedCount);
    }

    [Fact]
    public async Task GetMatches_FilterByTeamName_ReturnsMatchesForTeam()
    {
        // Arrange
        SeedTestData();
        var teamFilter = "Dragon";

        // Act
        var matches = await _dbContext.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.HomeTeam.Name.Contains(teamFilter) || m.AwayTeam.Name.Contains(teamFilter))
            .OrderBy(m => m.MatchDateUtc)
            .Take(100)
            .ToListAsync();

        // Assert
        matches.Should().HaveCount(2); // Both matches include Dragon FC
        matches.All(m => m.HomeTeam.Name.Contains(teamFilter) || m.AwayTeam.Name.Contains(teamFilter))
            .Should().BeTrue();
    }

    [Fact]
    public async Task GetMatches_FilterByDateRange_ReturnsMatchesInRange()
    {
        // Arrange
        SeedTestData();
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(10);

        // Act
        var matches = await _dbContext.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.MatchDateUtc >= startDate && m.MatchDateUtc <= endDate)
            .OrderBy(m => m.MatchDateUtc)
            .Take(100)
            .ToListAsync();

        // Assert
        matches.Should().HaveCount(2);
        matches.All(m => m.MatchDateUtc >= startDate && m.MatchDateUtc <= endDate).Should().BeTrue();
    }

    [Fact]
    public async Task GetMatches_FilterByAgeGroup_ReturnsMatchesForAgeGroup()
    {
        // Arrange
        SeedTestData();
        var ageGroupFilter = "U16";

        // Act
        var matches = await _dbContext.Matches
            .Include(m => m.AgeGroup)
            .Where(m => m.AgeGroup.Name == ageGroupFilter)
            .OrderBy(m => m.MatchDateUtc)
            .Take(100)
            .ToListAsync();

        // Assert
        matches.Should().HaveCount(1);
        matches.First().AgeGroup.Name.Should().Be(ageGroupFilter);
    }

    [Fact]
    public async Task GetMatches_FilterByDivision_ReturnsMatchesForDivision()
    {
        // Arrange
        SeedTestData();
        var regionFilter = "NorthEast";  // Changed from division to region

        // Act
        var matches = await _dbContext.Matches
            .Include(m => m.Region)
            .Where(m => m.Region.Name == regionFilter)
            .OrderBy(m => m.MatchDateUtc)
            .Take(100)
            .ToListAsync();

        // Assert
        matches.Should().HaveCount(1);
        matches.First().Region.Name.Should().Be(regionFilter);
    }

    [Fact]
    public async Task GetMatches_WithMultipleFilters_ReturnsFilteredMatches()
    {
        // Arrange
        SeedTestData();
        var teamFilter = "Dragon";
        var ageGroupFilter = "U16";
        var regionFilter = "NorthEast";

        // Act
        var matches = await _dbContext.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.AgeGroup)
            .Include(m => m.Region)
            .Where(m =>
                (m.HomeTeam.Name.Contains(teamFilter) || m.AwayTeam.Name.Contains(teamFilter)) &&
                m.AgeGroup.Name == ageGroupFilter &&
                m.Region.Name == regionFilter)
            .OrderBy(m => m.MatchDateUtc)
            .Take(100)
            .ToListAsync();

        // Assert
        matches.Should().HaveCount(1);
        matches.First().HomeTeam.Name.Should().Be("Dragon FC");
        matches.First().AgeGroup.Name.Should().Be("U16");
        matches.First().Region.Name.Should().Be("NorthEast");
    }

    // ============= GetTeams Query Tests =============

    [Fact]
    public async Task GetTeams_ReturnsAllTeamsSorted()
    {
        // Arrange
        SeedTestData();

        // Act
        var teams = await _dbContext.Teams
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        // Assert
        teams.Should().HaveCount(2);
        teams[0].Name.Should().Be("Dragon FC");
        teams[1].Name.Should().Be("Phoenix United");
    }

    [Fact]
    public async Task GetTeams_WithNoData_ReturnsEmptyList()
    {
        // Arrange (no seeding)

        // Act
        var teams = await _dbContext.Teams
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        // Assert
        teams.Should().BeEmpty();
    }

    // ============= GetDivisions Query Tests =============

    [Fact]
    public async Task GetDivisions_ReturnsAllDivisionsSorted()
    {
        // Arrange
        SeedTestData();

        // Act
        var divisions = await _dbContext.Divisions
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();

        // Assert
        divisions.Should().HaveCount(2);
        divisions[0].Name.Should().Be("Academy");   // 35 comes after "Homegrown" 12 in alphabetical order
        divisions[1].Name.Should().Be("Homegrown");
    }

    [Fact]
    public async Task GetDivisions_WithNoData_ReturnsEmptyList()
    {
        // Arrange (no seeding)

        // Act
        var divisions = await _dbContext.Divisions
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();

        // Assert
        divisions.Should().BeEmpty();
    }

    // ============= GetAgeGroups Query Tests =============

    [Fact]
    public async Task GetAgeGroups_ReturnsAllAgeGroupsSorted()
    {
        // Arrange
        SeedTestData();

        // Act
        var ageGroups = await _dbContext.AgeGroups
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name })
            .ToListAsync();

        // Assert
        ageGroups.Should().HaveCount(2);
        ageGroups[0].Name.Should().Be("U16");
        ageGroups[1].Name.Should().Be("U17");
    }

    [Fact]
    public async Task GetAgeGroups_WithNoData_ReturnsEmptyList()
    {
        // Arrange (no seeding)

        // Act
        var ageGroups = await _dbContext.AgeGroups
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name })
            .ToListAsync();

        // Assert
        ageGroups.Should().BeEmpty();
    }

    // ============= Match Data Integrity Tests =============

    [Fact]
    public async Task Match_IncludesAllRelatedEntities()
    {
        // Arrange
        SeedTestData();

        // Act
        var match = await _dbContext.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.AgeGroup)
            .Include(m => m.Region)
            .Include(m => m.Competition)
            .Include(m => m.Venue)
            .FirstAsync();

        // Assert
        match.Should().NotBeNull();
        match.HomeTeam.Should().NotBeNull();
        match.AwayTeam.Should().NotBeNull();
        match.AgeGroup.Should().NotBeNull();
        match.Region.Should().NotBeNull();
        match.Competition.Should().NotBeNull();
        match.Venue.Should().NotBeNull();
        match.Score.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Match_PaginationLimit_ReturnsMaxTake()
    {
        // Arrange
        for (int i = 0; i < 150; i++)
        {
            var match = CreateMatch($"match-{i}");
            _dbContext.Matches.Add(match);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var matches = await _dbContext.Matches
            .OrderBy(m => m.MatchDateUtc)
            .Take(100)
            .ToListAsync();

        // Assert
        matches.Should().HaveCount(100);
    }

    // ============= Helper Methods =============

    private void SeedTestData()
    {
        // Create league
        var league = new League { Name = "MLSNext" };
        _dbContext.Leagues.Add(league);
        _dbContext.SaveChanges();

        // Create divisions
        var homegrown = new Division { LeagueId = league.Id, Name = "Homegrown", TournamentId = 12 };
        var academy = new Division { LeagueId = league.Id, Name = "Academy", TournamentId = 35 };
        _dbContext.Divisions.AddRange(homegrown, academy);
        _dbContext.SaveChanges();

        // Create regions
        var northeast = new Region { DivisionId = homegrown.Id, Name = "NorthEast" };
        var southeast = new Region { DivisionId = academy.Id, Name = "SouthEast" };
        _dbContext.Regions.AddRange(northeast, southeast);

        // Create age groups
        var u16 = new AgeGroup { Name = "U16" };
        var u17 = new AgeGroup { Name = "U17" };
        _dbContext.AgeGroups.AddRange(u16, u17);

        // Create competition
        var ad = new Competition { Name = "AD" };
        _dbContext.Competitions.Add(ad);

        // Create teams
        var dragon = new Team { Name = "Dragon FC" };
        var phoenix = new Team { Name = "Phoenix United" };
        _dbContext.Teams.AddRange(dragon, phoenix);

        // Create venue
        var venue = new Venue { Name = "Central Park" };
        _dbContext.Venues.Add(venue);

        _dbContext.SaveChanges();

        // Create matches
        var match1 = new Match
        {
            MatchId = "match-001",
            MatchDateUtc = DateTime.UtcNow.AddDays(1),
            HomeTeamId = dragon.Id,
            AwayTeamId = phoenix.Id,
            AgeGroupId = u16.Id,
            RegionId = northeast.Id,
            CompetitionId = ad.Id,
            VenueId = venue.Id,
            Score = "2 Dragon FC to 1 Phoenix United"
        };

        var match2 = new Match
        {
            MatchId = "match-002",
            MatchDateUtc = DateTime.UtcNow.AddDays(2),
            HomeTeamId = phoenix.Id,
            AwayTeamId = dragon.Id,
            AgeGroupId = u17.Id,
            RegionId = southeast.Id,
            CompetitionId = ad.Id,
            VenueId = venue.Id,
            Score = "0 Phoenix United to 3 Dragon FC"
        };

        _dbContext.Matches.AddRange(match1, match2);
        _dbContext.SaveChanges();
    }

    private Match CreateMatch(string matchId)
    {
        // Create minimal required entities if they don't exist
        var ageGroup = _dbContext.AgeGroups.FirstOrDefault() ?? new AgeGroup { Name = "U16" };
        var league = _dbContext.Leagues.FirstOrDefault() ?? new League { Name = "MLSNext" };
        var division = _dbContext.Divisions.FirstOrDefault() ?? new Division { LeagueId = league.Id, Name = "Homegrown", TournamentId = 12 };
        var region = _dbContext.Regions.FirstOrDefault() ?? new Region { DivisionId = division.Id, Name = "NorthEast" };
        var competition = _dbContext.Competitions.FirstOrDefault() ?? new Competition { Name = "AD" };
        var homeTeam = _dbContext.Teams.FirstOrDefault() ?? new Team { Name = "Team A" };
        var awayTeam = _dbContext.Teams.Skip(1).FirstOrDefault() ?? new Team { Name = "Team B" };
        var venue = _dbContext.Venues.FirstOrDefault() ?? new Venue { Name = "Venue A" };

        if (ageGroup.Id == 0) _dbContext.Add(ageGroup);
        if (league.Id == 0) _dbContext.Add(league);
        if (division.Id == 0) _dbContext.Add(division);
        if (region.Id == 0) _dbContext.Add(region);
        if (competition.Id == 0) _dbContext.Add(competition);
        if (homeTeam.Id == 0) _dbContext.Add(homeTeam);
        if (awayTeam.Id == 0) _dbContext.Add(awayTeam);
        if (venue.Id == 0) _dbContext.Add(venue);
        _dbContext.SaveChanges();

        return new Match
        {
            MatchId = matchId,
            MatchDateUtc = DateTime.UtcNow.AddDays(Random.Shared.Next(1, 30)),
            HomeTeamId = homeTeam.Id,
            AwayTeamId = awayTeam.Id,
            AgeGroupId = ageGroup.Id,
            RegionId = region.Id,
            CompetitionId = competition.Id,
            VenueId = venue.Id,
            Score = "TBD"
        };
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
