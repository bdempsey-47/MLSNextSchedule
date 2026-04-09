using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using YSS.Functions.Services;

namespace YSS.Functions.Triggers;

public class ComputeHomepageSnapshot
{
    private readonly HomepageSnapshotService _snapshotService;
    private readonly ILogger _logger;

    public ComputeHomepageSnapshot(HomepageSnapshotService snapshotService, ILoggerFactory loggerFactory)
    {
        _snapshotService = snapshotService;
        _logger = loggerFactory.CreateLogger<ComputeHomepageSnapshot>();
    }

    // Nightly: 1 AM UTC (9 PM EDT) — after DailyIngestion, before peak traffic
    [Function("ComputeHomepageSnapshot")]
    public async Task Run([TimerTrigger("0 0 1 * * *")] TimerInfo myTimer, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("ComputeHomepageSnapshot triggered");

            if (myTimer.IsPastDue)
                _logger.LogWarning("ComputeHomepageSnapshot is running behind schedule");

            await _snapshotService.ComputeAndStoreAsync(ct);

            _logger.LogInformation("ComputeHomepageSnapshot completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ComputeHomepageSnapshot");
            throw;
        }
    }

    // Manual trigger: POST /api/TriggerHomepageSnapshot
    [Function("TriggerHomepageSnapshot")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Admin, "post", Route = "TriggerHomepageSnapshot")] HttpRequestData req,
        CancellationToken ct)
    {
        _logger.LogInformation("TriggerHomepageSnapshot HTTP trigger invoked");
        try
        {
            await _snapshotService.ComputeAndStoreAsync(ct);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteStringAsync("HomepageSnapshot computed successfully.");
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TriggerHomepageSnapshot");
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteStringAsync($"Error: {ex.Message}");
            return err;
        }
    }
}
