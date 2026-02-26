using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MLSNext.Data;
using Microsoft.EntityFrameworkCore;

namespace MLSNext.Functions.Triggers;

public class GetMatches
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public GetMatches(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<GetMatches>();
    }

    [Function("GetMatches")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "matches")] HttpRequestData req)
    {
        try
        {
            var query = req.Query["team"];
            var startDateStr = req.Query["startDate"];
            var endDateStr = req.Query["endDate"];
            var ageGroup = req.Query["ageGroup"];
            var division = req.Query["division"];

            var matches = _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Venue)
                .Include(m => m.AgeGroup)
                .Include(m => m.Division)
                .Include(m => m.Competition)
                .AsQueryable();

            // Filter by team
            if (!string.IsNullOrEmpty(query))
            {
                matches = matches.Where(m =>
                    m.HomeTeam.Name.Contains(query) ||
                    m.AwayTeam.Name.Contains(query));
            }

            // Filter by date range
            if (DateTime.TryParse(startDateStr, out var startDate))
            {
                matches = matches.Where(m => m.MatchDateUtc >= startDate);
            }

            if (DateTime.TryParse(endDateStr, out var endDate))
            {
                matches = matches.Where(m => m.MatchDateUtc <= endDate);
            }

            // Filter by age group
            if (!string.IsNullOrEmpty(ageGroup))
            {
                matches = matches.Where(m => m.AgeGroup.Name == ageGroup);
            }

            // Filter by division
            if (!string.IsNullOrEmpty(division))
            {
                matches = matches.Where(m => m.Division.Name == division);
            }

            var results = await matches
                .OrderBy(m => m.MatchDateUtc)
                .Take(100)
                .ToListAsync();

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(results);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in GetMatches: {ex.Message}");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}
