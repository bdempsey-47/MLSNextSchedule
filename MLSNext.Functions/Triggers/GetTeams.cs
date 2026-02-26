using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MLSNext.Data;
using Microsoft.EntityFrameworkCore;

namespace MLSNext.Functions.Triggers;

public class GetTeams
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public GetTeams(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<GetTeams>();
    }

    [Function("GetTeams")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teams")] HttpRequestData req)
    {
        try
        {
            var teams = await _context.Teams
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(teams);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in GetTeams: {ex.Message}");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}
