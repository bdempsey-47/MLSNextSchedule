using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MLSNext.Ingestion.Services;

namespace MLSNext.Functions.Triggers;

public class TriggerIngestion
{
    private readonly IngestionOrchestrator _orchestrator;
    private readonly ILogger _logger;

    public TriggerIngestion(IngestionOrchestrator orchestrator, ILoggerFactory loggerFactory)
    {
        _orchestrator = orchestrator;
        _logger = loggerFactory.CreateLogger<TriggerIngestion>();
    }

    [Function("TriggerIngestion")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ingestion/trigger")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Manual ingestion trigger initiated");

            var startTime = DateTime.UtcNow;
            await _orchestrator.RunAsync();
            var duration = DateTime.UtcNow - startTime;

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = "Ingestion completed",
                executionTimeMs = duration.TotalMilliseconds
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in TriggerIngestion: {ex.Message}");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}
