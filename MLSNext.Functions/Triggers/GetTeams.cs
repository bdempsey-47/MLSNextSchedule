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
            var program = req.Query["program"] ?? string.Empty;
            var season  = req.Query["season"]  ?? string.Empty;
            var region  = req.Query["region"]  ?? string.Empty;

            var matchQuery = _context.Matches
                .Include(m => m.Region).ThenInclude(r => r.Division)
                .AsQueryable();

            // Filter by program
            if (!string.IsNullOrEmpty(program))
            {
                if (program.ToLower() == "homegrown")
                    matchQuery = matchQuery.Where(m => m.Region.Division.TournamentId == 12);
                else if (program.ToLower() == "academy")
                    matchQuery = matchQuery.Where(m => m.Region.Division.TournamentId == 35);
            }

            // Filter by season
            var (seasonStart, seasonEnd) = ParseSeason(season);
            if (seasonStart.HasValue)
                matchQuery = matchQuery.Where(m => m.MatchDateUtc >= seasonStart.Value);
            if (seasonEnd.HasValue)
                matchQuery = matchQuery.Where(m => m.MatchDateUtc <= seasonEnd.Value);

            // Filter by region
            if (!string.IsNullOrEmpty(region))
                matchQuery = matchQuery.Where(m => m.Region.Name == region);

            // Return only teams that appear in the filtered matches.
            // Using two separate subqueries with OR avoids EF Core's inability to translate UNION-based Contains().
            var homeTeamIds = matchQuery.Select(m => m.HomeTeamId);
            var awayTeamIds = matchQuery.Select(m => m.AwayTeamId);

            var teams = await _context.Teams
                .Where(t => homeTeamIds.Contains(t.Id) || awayTeamIds.Contains(t.Id))
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            _logger.LogInformation("GetTeams returning {Count} teams (program={Program}, season={Season}, region={Region})",
                teams.Count, program, season, string.IsNullOrEmpty(region) ? "(all)" : region);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await response.WriteAsJsonAsync(teams);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in GetTeams: {ex.Message}");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            errorResponse.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            errorResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    private (DateTime? StartDate, DateTime? EndDate) ParseSeason(string? season)
    {
        return season?.ToLower() switch
        {
            "fall2025"   => (new DateTime(2025, 7, 1), new DateTime(2025, 12, 31, 23, 59, 59)),
            "spring2026" => (new DateTime(2026, 1, 1), new DateTime(2026, 6, 30, 23, 59, 59)),
            _ => (null, null)
        };
    }
}
