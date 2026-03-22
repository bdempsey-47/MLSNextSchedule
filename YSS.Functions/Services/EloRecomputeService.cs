using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YSS.Data;

namespace YSS.Functions.Services;

public class EloRecomputeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<EloRecomputeService> _logger;

    // No longer used for filtering (program is now on Team entity), kept for reference
    // Academy competitions all start with "AD" (AD, AD Showcase, AD Group Play)

    public EloRecomputeService(AppDbContext context, ILogger<EloRecomputeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Recompute ELO ratings for all teams across all programs and age groups,
    /// then batch-update the Team.EloRating column.
    /// </summary>
    public async Task RecomputeAllAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting ELO recomputation");

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
                TournamentId = m.Region.Division.TournamentId,
                CompetitionName = m.Competition.Name,
                AgeGroupName = m.AgeGroup.Name
            })
            .ToListAsync(ct);

        _logger.LogInformation("Loaded {Count} completed matches for ELO computation", allMatches.Count);

        // Build ELO inputs partitioned by program (AG vs HG get separate ELO pools)
        var agInputs = new List<EloCalculator.EloMatchInput>();
        var hgInputs = new List<EloCalculator.EloMatchInput>();

        foreach (var m in allMatches)
        {
            if (!TryParseScore(m.Score!, out var homeScore, out var awayScore))
                continue;

            var input = new EloCalculator.EloMatchInput(
                m.HomeTeamId, m.AwayTeamId, homeScore, awayScore, m.MatchDateUtc);

            // Determine program from home team (both teams in a match share the same program)
            var program = teamPrograms.GetValueOrDefault(m.HomeTeamId, "HG");
            if (program == "AG")
                agInputs.Add(input);
            else
                hgInputs.Add(input);
        }

        _logger.LogInformation("Built {AgCount} AG and {HgCount} HG ELO inputs", agInputs.Count, hgInputs.Count);

        // Compute ratings separately per program pool
        var agResults = EloCalculator.ComputeRankings(agInputs);
        var hgResults = EloCalculator.ComputeRankings(hgInputs);

        // Merge results
        var results = new Dictionary<int, EloCalculator.EloTeamState>();
        foreach (var kvp in agResults) results[kvp.Key] = kvp.Value;
        foreach (var kvp in hgResults) results[kvp.Key] = kvp.Value;

        // Batch-update team ratings
        var teams = await _context.Teams.ToListAsync(ct);
        var updated = 0;

        foreach (var team in teams)
        {
            var newRating = results.TryGetValue(team.Id, out var state)
                ? (int)Math.Round(state.Rating)
                : 1500;

            if (team.EloRating != newRating)
            {
                team.EloRating = newRating;
                updated++;
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("ELO recomputation complete: {Updated} teams updated out of {Total}",
            updated, teams.Count);
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
