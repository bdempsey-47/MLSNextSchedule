using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MLSNext.Data;
using Microsoft.EntityFrameworkCore;

namespace MLSNext.Functions.Triggers;

public class GetRegions
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public GetRegions(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<GetRegions>();
    }

    [Function("GetRegions")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "regions")] HttpRequestData req)
    {
        try
        {
            // Optionally filter by division name (e.g., "Academy", "Homegrown")
            var divisionFilter = req.Query["division"];

            var regions = _context.Regions
                .Include(r => r.Division)
                .OrderBy(r => r.Name)
                .AsQueryable();

            // If division filter is provided, filter by division name
            if (!string.IsNullOrEmpty(divisionFilter))
            {
                regions = regions.Where(r => r.Division.Name == divisionFilter);
            }

            var results = await regions
                .Select(r => new { r.Id, r.Name, Division = new { r.Division.Id, r.Division.Name, r.Division.TournamentId } })
                .Distinct()
                .ToListAsync();

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await response.WriteAsJsonAsync(results);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in GetRegions: {ex.Message}");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            errorResponse.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            errorResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}
