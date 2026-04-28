using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using YSS.Functions.Models;
using YSS.Functions.Services;
using YSS.Ingestion.Services;

namespace YSS.Functions.Triggers;

public class ScheduledIngestion
{
    private readonly IngestionOrchestrator _orchestrator;
    private readonly List<TournamentSeason> _seasons;
    private readonly EloRecomputeService _eloService;
    private readonly ILogger _logger;

    public ScheduledIngestion(
        IngestionOrchestrator orchestrator,
        List<TournamentSeason> seasons,
        EloRecomputeService eloService,
        ILoggerFactory loggerFactory)
    {
        _orchestrator = orchestrator;
        _seasons = seasons;
        _eloService = eloService;
        _logger = loggerFactory.CreateLogger<ScheduledIngestion>();
    }

    // Nightly: 2 AM UTC (10 PM EDT) every day
    [Function("DailyIngestion")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo myTimer, CancellationToken ct)
        => await RunIngestion("Daily", myTimer, ct);

    // Weekend midday: 4 PM UTC (12 PM EDT) Saturday + Sunday
    [Function("WeekendIngestion_Noon")]
    public async Task RunWeekendNoon([TimerTrigger("0 0 16 * * 0,6")] TimerInfo myTimer, CancellationToken ct)
        => await RunIngestion("Weekend-Noon", myTimer, ct);

    // Weekend evening: 9 PM UTC (5 PM EDT) Saturday + Sunday
    [Function("WeekendIngestion_Evening")]
    public async Task RunWeekendEvening([TimerTrigger("0 0 21 * * 0,6")] TimerInfo myTimer, CancellationToken ct)
        => await RunIngestion("Weekend-Evening", myTimer, ct);

    private async Task RunIngestion(string label, TimerInfo myTimer, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var start = now.AddDays(-14).ToString("yyyy-MM-dd 00:00:01");
        var end   = now.AddDays(14).ToString("yyyy-MM-dd 23:59:59");

        _logger.LogInformation("{Label} ingestion started at {Now:O} — window: {Start} to {End}", label, now, start, end);

        if (myTimer.IsPastDue)
            _logger.LogWarning("{Label} ingestion is running behind schedule", label);

        var activeSeasons = _seasons.Where(s => s.IsActive(now)).ToList();
        if (activeSeasons.Count == 0)
        {
            _logger.LogWarning("No active seasons found for {Date:yyyy-MM-dd}. Check TournamentSeason registrations in Program.cs.", now);
            return;
        }

        foreach (var season in activeSeasons)
        {
            _logger.LogInformation("Processing season: {Label} (tournament {TournamentId}) [{RunLabel}]",
                season.Label, season.TournamentId, label);

            try
            {
                await _orchestrator.RunAsync(ct, leagueName: season.LeagueName, startDate: start, endDate: end, tournamentId: season.TournamentId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during {Label} ingestion for season {SeasonLabel}", label, season.Label);
                throw;
            }
        }

        // Recompute ELO ratings after ingestion
        try
        {
            await _eloService.RecomputeAllAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ELO recomputation after {Label} ingestion", label);
        }

        _logger.LogInformation("{Label} ingestion complete", label);
    }
}
