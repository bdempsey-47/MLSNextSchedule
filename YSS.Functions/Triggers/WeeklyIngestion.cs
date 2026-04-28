using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using YSS.Functions.Models;
using YSS.Ingestion.Services;

namespace YSS.Functions.Triggers;

public class WeeklyIngestion
{
    private static readonly List<string> Batch1 = ["21", "22"]; // u13, u14
    private static readonly List<string> Batch2 = ["33", "14"]; // u15, u16
    private static readonly List<string> Batch3 = ["15", "26"]; // u17, u19

    private readonly IngestionOrchestrator _orchestrator;
    private readonly List<TournamentSeason> _seasons;
    private readonly ILogger _logger;

    public WeeklyIngestion(
        IngestionOrchestrator orchestrator,
        List<TournamentSeason> seasons,
        ILoggerFactory loggerFactory)
    {
        _orchestrator = orchestrator;
        _seasons = seasons;
        _logger = loggerFactory.CreateLogger<WeeklyIngestion>();
    }

    [Function("WeeklyIngestion_u13u14")]
    public async Task RunBatch1([TimerTrigger("0 0 3 * * 0")] TimerInfo myTimer, CancellationToken ct)
        => await RunBatch("u13+u14", Batch1, myTimer, ct);

    [Function("WeeklyIngestion_u15u16")]
    public async Task RunBatch2([TimerTrigger("0 30 3 * * 0")] TimerInfo myTimer, CancellationToken ct)
        => await RunBatch("u15+u16", Batch2, myTimer, ct);

    [Function("WeeklyIngestion_u17u19")]
    public async Task RunBatch3([TimerTrigger("0 0 4 * * 0")] TimerInfo myTimer, CancellationToken ct)
        => await RunBatch("u17+u19", Batch3, myTimer, ct);

    private async Task RunBatch(string batchLabel, List<string> ageGroups, TimerInfo myTimer, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var start = now.AddDays(-30).ToString("yyyy-MM-dd 00:00:01");

        _logger.LogInformation("Weekly true-up [{Batch}] started at {Now:O} — from {Start} to season end", batchLabel, now, start);

        if (myTimer.IsPastDue)
            _logger.LogWarning("Weekly ingestion [{Batch}] is running behind schedule", batchLabel);

        var activeSeasons = _seasons.Where(s => s.IsActive(now)).ToList();
        if (activeSeasons.Count == 0)
        {
            _logger.LogWarning("No active seasons found for {Date:yyyy-MM-dd}. Check TournamentSeason registrations in Program.cs.", now);
            return;
        }

        foreach (var season in activeSeasons)
        {
            var end = season.SeasonEnd.ToString("yyyy-MM-dd 23:59:59");
            _logger.LogInformation("Processing season: {Label} (tournament {TournamentId}) [{Batch}] — window: {Start} to {End}",
                season.Label, season.TournamentId, batchLabel, start, end);

            try
            {
                await _orchestrator.RunAsync(ct, leagueName: season.LeagueName, startDate: start, endDate: end, ageGroups: ageGroups, tournamentId: season.TournamentId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during weekly ingestion [{Batch}] for season {Label}", batchLabel, season.Label);
                throw;
            }
        }

        _logger.LogInformation("Weekly ingestion [{Batch}] complete", batchLabel);
    }
}
