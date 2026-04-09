using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using YSS.Functions.Services;
using YSS.Ingestion.Services;

namespace YSS.Functions.Triggers;

public class TriggerIngestion
{
    private readonly IngestionOrchestrator _orchestrator;
    private readonly EloRecomputeService _eloService;
    private readonly ILogger _logger;

    public TriggerIngestion(IngestionOrchestrator orchestrator, EloRecomputeService eloService, ILoggerFactory loggerFactory)
    {
        _orchestrator = orchestrator;
        _eloService = eloService;
        _logger = loggerFactory.CreateLogger<TriggerIngestion>();
    }

    [Function("TriggerIngestion")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ingestion/trigger")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Manual ingestion trigger initiated");

            var startTime = DateTime.UtcNow;
            await _orchestrator.RunAsync();
            await _eloService.RecomputeAllAsync(CancellationToken.None);
            var duration = DateTime.UtcNow - startTime;

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
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
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            errorResponse.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            errorResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}
