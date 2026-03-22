using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YSS.Data;

namespace YSS.Functions.Services;

public class EloRecomputeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<EloRecomputeService> _logger;

    private static readonly string[] AcademyCompetitions = { "AD Showcase", "AD" };

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

        // Build ELO inputs from all matches (one unified computation across all programs/age groups)
        var eloInputs = new List<EloCalculator.EloMatchInput>();

        foreach (var m in allMatches)
        {
            if (!TryParseScore(m.Score!, out var homeScore, out var awayScore))
                continue;

            eloInputs.Add(new EloCalculator.EloMatchInput(
                m.HomeTeamId, m.AwayTeamId, homeScore, awayScore, m.MatchDateUtc));
        }

        _logger.LogInformation("Built {Count} ELO inputs from parsed matches", eloInputs.Count);

        // Compute ratings
        var results = EloCalculator.ComputeRankings(eloInputs);

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
