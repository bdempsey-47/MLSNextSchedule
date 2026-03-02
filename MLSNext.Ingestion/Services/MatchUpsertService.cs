using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLSNext.Data;
using MLSNext.Data.Entities;
using MLSNext.Ingestion.Models;

namespace MLSNext.Ingestion.Services;

/// <summary>
/// Handles database operations for upserting matches and their related entities.
/// Uses lookup-or-create pattern for reference tables (Teams, Venues, etc.).
/// </summary>
public class MatchUpsertService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<MatchUpsertService> _logger;

    public MatchUpsertService(AppDbContext dbContext, ILogger<MatchUpsertService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Upsert a collection of parsed matches into the database.
    /// </summary>
    /// <param name="parsedMatches">Collection of matches to upsert</param>
    /// <param name="leagueName">Name of the league (e.g., 'MLS Next', 'ECNL', 'EDP')</param>
    /// <param name="ct">Cancellation token</param>
    public async Task UpsertMatchesAsync(List<ParsedMatch> parsedMatches, string leagueName = "MLS Next", CancellationToken ct = default)
    {
        _logger.LogInformation("Upserting {Count} matches", parsedMatches.Count);

        var newMatches = 0;
        var updatedMatches = 0;
        var duplicateMatches = 0;

        foreach (var parsedMatch in parsedMatches)
        {
            try
            {
                // Check if match already exists
                var existingMatch = await _dbContext.Matches
                    .Where(m => m.MatchId == parsedMatch.MatchId)
                    .FirstOrDefaultAsync(ct);

                if (existingMatch != null)
                {
                    // Update existing match score and team logos
                    existingMatch.Score = parsedMatch.Score;
                    existingMatch.UpdatedAt = DateTime.UtcNow;
                    // Refresh logos in case they were added/changed since initial ingestion
                    await LookupOrCreateTeamAsync(parsedMatch.HomeTeamName, parsedMatch.HomeTeamLogoUrl, ct);
                    await LookupOrCreateTeamAsync(parsedMatch.AwayTeamName, parsedMatch.AwayTeamLogoUrl, ct);
                    updatedMatches++;
                }
                else
                {
                    // Create new match with lookup-or-create for reference entities
                    var homeTeam = await LookupOrCreateTeamAsync(parsedMatch.HomeTeamName, parsedMatch.HomeTeamLogoUrl, ct);
                    var awayTeam = await LookupOrCreateTeamAsync(parsedMatch.AwayTeamName, parsedMatch.AwayTeamLogoUrl, ct);
                    var venue = await LookupOrCreateVenueAsync(parsedMatch.VenueName, ct);
                    var division = await LookupOrCreateDivisionAsync(parsedMatch.TournamentId, leagueName, ct);
                    var region = await LookupOrCreateRegionAsync(division.Id, parsedMatch.Division, ct);
                    var competition = await LookupOrCreateCompetitionAsync(parsedMatch.Competition, ct);
                    var ageGroup = await LookupOrCreateAgeGroupAsync(parsedMatch.AgeGroup, ct);

                    var match = new Match
                    {
                        MatchId = parsedMatch.MatchId,
                        HomeTeamId = homeTeam.Id,
                        AwayTeamId = awayTeam.Id,
                        VenueId = venue.Id,
                        RegionId = region.Id,
                        CompetitionId = competition.Id,
                        AgeGroupId = ageGroup.Id,
                        MatchDateUtc = parsedMatch.MatchDate,
                        Score = parsedMatch.Score,
                        Gender = parsedMatch.Gender,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _dbContext.Matches.AddAsync(match, ct);
                    newMatches++;
                }
            }
            catch (InvalidOperationException)
            {
                // Duplicate Match ID already in this batch
                duplicateMatches++;
                _logger.LogWarning("Duplicate match ID in batch: {MatchId}", parsedMatch.MatchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting match {MatchId}", parsedMatch.MatchId);
            }
        }

        // Save all changes in a single transaction
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Upsert complete: {NewMatches} new, {UpdatedMatches} updated, {DuplicateMatches} duplicates",
            newMatches, updatedMatches, duplicateMatches);
    }

    private async Task<Team> LookupOrCreateTeamAsync(string name, string? logoUrl, CancellationToken ct)
    {
        var team = await _dbContext.Teams
            .Where(t => t.Name == name)
            .FirstOrDefaultAsync(ct);

        if (team == null)
        {
            team = new Team { Name = name, LogoUrl = logoUrl };
            await _dbContext.Teams.AddAsync(team, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Created new team: {TeamName}", name);
        }
        else if (logoUrl != null && team.LogoUrl != logoUrl)
        {
            // Update logo URL if we have a newer/different one
            team.LogoUrl = logoUrl;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Updated logo for team: {TeamName}", name);
        }

        return team;
    }

    private async Task<Venue> LookupOrCreateVenueAsync(string name, CancellationToken ct)
    {
        var venue = await _dbContext.Venues
            .Where(v => v.Name == name)
            .FirstOrDefaultAsync(ct);

        if (venue == null)
        {
            venue = new Venue { Name = name };
            await _dbContext.Venues.AddAsync(venue, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Created new venue: {VenueName}", name);
        }

        return venue;
    }

    private async Task<Division> LookupOrCreateDivisionAsync(int tournamentId, string leagueName, CancellationToken ct)
    {
        // Map tournament ID to division name: 12=Homegrown, 35=Academy
        var divisionName = tournamentId switch
        {
            12 => "Homegrown",
            35 => "Academy",
            _ => throw new InvalidOperationException($"Unknown tournament ID: {tournamentId}")
        };

        var division = await _dbContext.Divisions
            .Where(d => d.TournamentId == tournamentId)
            .FirstOrDefaultAsync(ct);

        if (division == null)
        {
            // Look up the specified league
            var league = await _dbContext.Leagues
                .Where(l => l.Name == leagueName)
                .FirstOrDefaultAsync(ct);
            
            if (league == null)
            {
                throw new InvalidOperationException($"League '{leagueName}' not found. Ensure the league is seeded in the database.");
            }

            division = new Division 
            { 
                LeagueId = league.Id,
                Name = divisionName,
                TournamentId = tournamentId
            };
            await _dbContext.Divisions.AddAsync(division, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Created {DivisionName} division for tournament {TournamentId} in league {LeagueName}", divisionName, tournamentId, leagueName);
        }

        return division;
    }

    private async Task<Region> LookupOrCreateRegionAsync(int divisionId, string name, CancellationToken ct)
    {
        var region = await _dbContext.Regions
            .Where(r => r.DivisionId == divisionId && r.Name == name)
            .FirstOrDefaultAsync(ct);

        if (region == null)
        {
            region = new Region { DivisionId = divisionId, Name = name };
            await _dbContext.Regions.AddAsync(region, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Created new region: {RegionName} in division {DivisionId}", name, divisionId);
        }

        return region;
    }

    private async Task<Competition> LookupOrCreateCompetitionAsync(string name, CancellationToken ct)
    {
        var competition = await _dbContext.Competitions
            .Where(c => c.Name == name)
            .FirstOrDefaultAsync(ct);

        if (competition == null)
        {
            competition = new Competition { Name = name };
            await _dbContext.Competitions.AddAsync(competition, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Created new competition: {CompetitionName}", name);
        }

        return competition;
    }

    private async Task<AgeGroup> LookupOrCreateAgeGroupAsync(string name, CancellationToken ct)
    {
        var ageGroup = await _dbContext.AgeGroups
            .Where(a => a.Name == name)
            .FirstOrDefaultAsync(ct);

        if (ageGroup == null)
        {
            ageGroup = new AgeGroup { Name = name };
            await _dbContext.AgeGroups.AddAsync(ageGroup, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Created new age group: {AgeGroupName}", name);
        }

        return ageGroup;
    }
}
