using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using YSS.Functions.Models;
using YSS.Ingestion.Services;

namespace YSS.Functions.Triggers;

public class WeeklyIngestion
{
    private readonly IngestionOrchestrator _orchestrator;
    private readonly Modular11Settings _settings;
    private readonly List<TournamentSeason> _seasons;
    private readonly ILogger _logger;

    public WeeklyIngestion(
        IngestionOrchestrator orchestrator,
        Modular11Settings settings,
        List<TournamentSeason> seasons,
        ILoggerFactory loggerFactory)
    {
        _orchestrator = orchestrator;
        _settings = settings;
        _seasons = seasons;
        _logger = loggerFactory.CreateLogger<WeeklyIngestion>();
    }

    [Function("WeeklyIngestion")]
    public async Task Run([TimerTrigger("0 0 3 * * 0")] TimerInfo myTimer, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var start = now.AddDays(-30).ToString("yyyy-MM-dd 00:00:01");

        _logger.LogInformation("Weekly true-up ingestion started at {Now:O} — from {Start} to season end", now, start);

        if (myTimer.IsPastDue)
            _logger.LogWarning("Weekly ingestion is running behind schedule");

        var activeSeasons = _seasons.Where(s => s.IsActive(now)).ToList();
        if (activeSeasons.Count == 0)
        {
            _logger.LogWarning("No active seasons found for {Date:yyyy-MM-dd}. Check TournamentSeason registrations in Program.cs.", now);
            return;
        }

        foreach (var season in activeSeasons)
        {
            var end = season.SeasonEnd.ToString("yyyy-MM-dd 23:59:59");
            _logger.LogInformation("Processing season: {Label} (tournament {TournamentId}) — window: {Start} to {End}",
                season.Label, season.TournamentId, start, end);
            _settings.TournamentId = season.TournamentId;

            try
            {
                await _orchestrator.RunAsync(ct, leagueName: season.LeagueName, startDate: start, endDate: end);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during weekly ingestion for season {Label}", season.Label);
                throw;
            }
        }

        _logger.LogInformation("Weekly ingestion complete");
    }
}
