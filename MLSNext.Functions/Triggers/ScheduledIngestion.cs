using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MLSNext.Ingestion.Services;

namespace MLSNext.Functions.Triggers;

public class ScheduledIngestion
{
    private readonly IngestionOrchestrator _orchestrator;
    private readonly ILogger _logger;

    public ScheduledIngestion(IngestionOrchestrator orchestrator, ILoggerFactory loggerFactory)
    {
        _orchestrator = orchestrator;
        _logger = loggerFactory.CreateLogger<ScheduledIngestion>();
    }

    [Function("ScheduledIngestion")]
    public async Task Run([TimerTrigger("0 0 */4 * * *")] TimerInfo myTimer)
    {
        try
        {
            _logger.LogInformation($"Scheduled ingestion started at {DateTime.UtcNow:O}");

            var startTime = DateTime.UtcNow;
            await _orchestrator.RunAsync();
            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation($"Ingestion completed in {duration.TotalMilliseconds}ms");

            if (myTimer.IsPastDue)
            {
                _logger.LogWarning("Scheduled ingestion running behind schedule");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in ScheduledIngestion: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }
}
