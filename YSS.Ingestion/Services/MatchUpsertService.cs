using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YSS.Data;
using YSS.Data.Entities;
using YSS.Ingestion.Models;

namespace YSS.Ingestion.Services;

/// <summary>
/// Handles database operations for upserting matches and their related entities.
/// Uses lookup-or-create pattern for reference tables (Teams, Venues, etc.).
/// </summary>
public class MatchUpsertService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<MatchUpsertService> _logger;

    // Per-run in-memory caches — avoids repeated DB lookups for the same reference entities
    private readonly Dictionary<string, Team> _teamCache = new();
    private readonly Dictionary<string, Venue> _venueCache = new();
    private readonly Dictionary<int, Division> _divisionCache = new();
    private readonly Dictionary<(int DivisionId, string Name), Region> _regionCache = new();
    private readonly Dictionary<string, Competition> _competitionCache = new();
    private readonly Dictionary<string, AgeGroup> _ageGroupCache = new();

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

        // Check for duplicate IDs in the batch
        var duplicateIds = parsedMatches
            .GroupBy(m => m.MatchId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Any())
        {
            _logger.LogWarning("Found {DuplicateCount} duplicate match IDs in the batch: {DuplicateIds}",
                duplicateIds.Count, string.Join(", ", duplicateIds));
        }

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
                    // Update all mutable fields on existing match
                    var homeTeam = await LookupOrCreateTeamAsync(parsedMatch.HomeTeamName, parsedMatch.HomeTeamLogoUrl, ct);
                    var awayTeam = await LookupOrCreateTeamAsync(parsedMatch.AwayTeamName, parsedMatch.AwayTeamLogoUrl, ct);
                    var venue = await LookupOrCreateVenueAsync(parsedMatch.VenueName, ct);
                    var division = await LookupOrCreateDivisionAsync(parsedMatch.TournamentId, leagueName, ct);
                    var region = await LookupOrCreateRegionAsync(division.Id, parsedMatch.Division, ct);
                    var competition = await LookupOrCreateCompetitionAsync(parsedMatch.Competition, ct);
                    var ageGroup = await LookupOrCreateAgeGroupAsync(parsedMatch.AgeGroup, ct);

                    existingMatch.MatchDateUtc = parsedMatch.MatchDate;
                    existingMatch.HomeTeamId = homeTeam.Id;
                    existingMatch.AwayTeamId = awayTeam.Id;
                    existingMatch.VenueId = venue.Id;
                    existingMatch.RegionId = region.Id;
                    existingMatch.CompetitionId = competition.Id;
                    existingMatch.AgeGroupId = ageGroup.Id;
                    existingMatch.Gender = parsedMatch.Gender;
                    existingMatch.Score = parsedMatch.Score;
                    existingMatch.UpdatedAt = DateTime.UtcNow;
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
        if (_teamCache.TryGetValue(name, out var cached))
        {
            if (logoUrl != null && cached.LogoUrl != logoUrl)
            {
                cached.LogoUrl = logoUrl;
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogDebug("Updated logo for team: {TeamName}", name);
            }
            return cached;
        }

        var team = await _dbContext.Teams
            .Where(t => t.Name == name.Trim())
            .FirstOrDefaultAsync(ct);

        if (team == null)
        {
            team = new Team { Name = name.Trim(), LogoUrl = logoUrl };
            await _dbContext.Teams.AddAsync(team, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Created new team: {TeamName}", name);
        }
        else if (logoUrl != null && team.LogoUrl != logoUrl)
        {
            team.LogoUrl = logoUrl;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Updated logo for team: {TeamName}", name);
        }

        _teamCache[name] = team;
        return team;
    }

    private async Task<Venue> LookupOrCreateVenueAsync(string name, CancellationToken ct)
    {
        if (_venueCache.TryGetValue(name, out var cached))
            return cached;

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

        _venueCache[name] = venue;
        return venue;
    }

    private async Task<Division> LookupOrCreateDivisionAsync(int tournamentId, string leagueName, CancellationToken ct)
    {
        if (_divisionCache.TryGetValue(tournamentId, out var cached))
            return cached;

        // Map tournament ID to division name: 12=Homegrown, 35=Academy, 75=FEST (Homegrown)
        var divisionName = tournamentId switch
        {
            12 => "Homegrown",
            35 => "Academy",
            75 => "Homegrown",    // FEST (Pro Player Pathway)
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

        _divisionCache[tournamentId] = division;
        return division;
    }

    private async Task<Region> LookupOrCreateRegionAsync(int divisionId, string name, CancellationToken ct)
    {
        var key = (divisionId, name);
        if (_regionCache.TryGetValue(key, out var cached))
            return cached;

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

        _regionCache[key] = region;
        return region;
    }

    private async Task<Competition> LookupOrCreateCompetitionAsync(string name, CancellationToken ct)
    {
        if (_competitionCache.TryGetValue(name, out var cached))
            return cached;

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

        _competitionCache[name] = competition;
        return competition;
    }

    private async Task<AgeGroup> LookupOrCreateAgeGroupAsync(string name, CancellationToken ct)
    {
        if (_ageGroupCache.TryGetValue(name, out var cached))
            return cached;

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

        _ageGroupCache[name] = ageGroup;
        return ageGroup;
    }
}
