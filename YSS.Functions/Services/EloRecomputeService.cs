using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YSS.Data;
using YSS.Data.Entities;

namespace YSS.Functions.Services;

public class EloRecomputeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<EloRecomputeService> _logger;

    public EloRecomputeService(AppDbContext context, ILogger<EloRecomputeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Recompute ELO ratings per (program, ageGroup) pool and store in TeamAgeGroupElos table.
    /// </summary>
    public async Task RecomputeAllAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting ELO recomputation (per age group)");

        var oneYearAgo = DateTime.UtcNow.AddYears(-1);

        // Load team program lookup
        var teamPrograms = await _context.Teams
            .Select(t => new { t.Id, t.Program })
            .ToDictionaryAsync(t => t.Id, t => t.Program, ct);

        // Load all completed matches with scores in the rolling window
        var allMatches = await _context.Matches
            .Include(m => m.Region).ThenInclude(r => r.Division)
            .Include(m => m.Competition)
            .Include(m => m.AgeGroup)
            .Where(m =>
                m.Score != null && m.Score != "" && m.Score != "TBD" &&
                m.MatchDateUtc >= oneYearAgo)
            .OrderBy(m => m.MatchDateUtc)
            .Select(m => new
            {
                m.HomeTeamId,
                m.AwayTeamId,
                m.Score,
                m.MatchDateUtc,
                m.AgeGroupId,
                AgeGroupName = m.AgeGroup.Name
            })
            .ToListAsync(ct);

        _logger.LogInformation("Loaded {Count} completed matches for ELO computation", allMatches.Count);

        // Partition matches by (program, ageGroupId) — each pool gets its own ELO computation
        var pools = new Dictionary<(string Program, int AgeGroupId), List<EloCalculator.EloMatchInput>>();

        foreach (var m in allMatches)
        {
            if (!TryParseScore(m.Score!, out var homeScore, out var awayScore))
                continue;

            var program = teamPrograms.GetValueOrDefault(m.HomeTeamId, "HG");
            var key = (program, m.AgeGroupId);

            if (!pools.ContainsKey(key))
                pools[key] = new List<EloCalculator.EloMatchInput>();

            pools[key].Add(new EloCalculator.EloMatchInput(
                m.HomeTeamId, m.AwayTeamId, homeScore, awayScore, m.MatchDateUtc));
        }

        _logger.LogInformation("Built {PoolCount} ELO pools across programs and age groups", pools.Count);

        // Compute ELO per pool and collect all (teamId, ageGroupId) → rating
        var allRatings = new Dictionary<(int TeamId, int AgeGroupId), int>();

        foreach (var (key, inputs) in pools)
        {
            var results = EloCalculator.ComputeRankings(inputs);
            foreach (var (teamId, state) in results)
            {
                allRatings[(teamId, key.AgeGroupId)] = (int)Math.Round(state.Rating);
            }
        }

        // Load existing TeamAgeGroupElo rows
        var existing = await _context.TeamAgeGroupElos.ToListAsync(ct);
        var existingLookup = existing.ToDictionary(e => (e.TeamId, e.AgeGroupId));

        var created = 0;
        var updated = 0;

        foreach (var ((teamId, ageGroupId), rating) in allRatings)
        {
            if (existingLookup.TryGetValue((teamId, ageGroupId), out var elo))
            {
                if (elo.EloRating != rating)
                {
                    elo.EloRating = rating;
                    updated++;
                }
            }
            else
            {
                _context.TeamAgeGroupElos.Add(new TeamAgeGroupElo
                {
                    TeamId = teamId,
                    AgeGroupId = ageGroupId,
                    EloRating = rating
                });
                created++;
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ELO recomputation complete: {Created} created, {Updated} updated across {Pools} pools",
            created, updated, pools.Count);
    }

    private static bool TryParseScore(string score, out int homeScore, out int awayScore)
    {
        homeScore = 0;
        awayScore = 0;
        if (string.IsNullOrWhiteSpace(score)) return false;
        var parts = score.Split('-');
        if (parts.Length != 2) return false;
        return int.TryParse(parts[0].Trim(), out homeScore) &&
               int.TryParse(parts[1].Trim(), out awayScore);
    }
}
