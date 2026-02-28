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
            var query = string.IsNullOrEmpty(req.Query["team"]) ? string.Empty : req.Query["team"].ToString();
            var season = string.IsNullOrEmpty(req.Query["season"]) ? string.Empty : req.Query["season"].ToString();
            var startDateStr = string.IsNullOrEmpty(req.Query["startDate"]) ? string.Empty : req.Query["startDate"].ToString();
            var endDateStr = string.IsNullOrEmpty(req.Query["endDate"]) ? string.Empty : req.Query["endDate"].ToString();
            var ageGroup = string.IsNullOrEmpty(req.Query["ageGroup"]) ? string.Empty : req.Query["ageGroup"].ToString();
            var division = string.IsNullOrEmpty(req.Query["division"]) ? string.Empty : req.Query["division"].ToString();
            var program = string.IsNullOrEmpty(req.Query["program"]) ? string.Empty : req.Query["program"].ToString();

            _logger.LogInformation("GetMatches called with: season={Season}, program={Program}, startDate={StartDate}, endDate={EndDate}", 
                season ?? "(none)", program ?? "(none)", startDateStr ?? "(none)", endDateStr ?? "(none)");

            var matches = _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Venue)
                .Include(m => m.AgeGroup)
                .Include(m => m.Region)
                .ThenInclude(r => r.Division)
                .Include(m => m.Competition)
                .AsQueryable();

            // Filter by program (homegrown=12, academy=35)
            if (!string.IsNullOrEmpty(program))
            {
                if (program.ToLower() == "homegrown")
                {
                    _logger.LogInformation("Filtering by program: Homegrown (TournamentId=12)");
                    matches = matches.Where(m => m.Region.Division.TournamentId == 12);
                }
                else if (program.ToLower() == "academy")
                {
                    _logger.LogInformation("Filtering by program: Academy (TournamentId=35)");
                    matches = matches.Where(m => m.Region.Division.TournamentId == 35);
                }
            }

            // Map season to date range
            var (seasonStartDate, seasonEndDate) = ParseSeason(season);

            // Filter by team
            if (!string.IsNullOrEmpty(query))
            {
                matches = matches.Where(m =>
                    m.HomeTeam.Name.Contains(query) ||
                    m.AwayTeam.Name.Contains(query));
            }

            // Filter by season (if provided)
            if (seasonStartDate.HasValue)
            {
                _logger.LogInformation("Applying season start date filter: {StartDate}", seasonStartDate);
                matches = matches.Where(m => m.MatchDateUtc >= seasonStartDate.Value);
            }

            if (seasonEndDate.HasValue)
            {
                _logger.LogInformation("Applying season end date filter: {EndDate}", seasonEndDate);
                matches = matches.Where(m => m.MatchDateUtc <= seasonEndDate.Value);
            }

            // Allow explicit startDate/endDate to override season
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

            // Filter by division (region name)
            if (!string.IsNullOrEmpty(division))
            {
                matches = matches.Where(m => m.Region.Name == division);
            }

            var results = await matches
                .OrderBy(m => m.MatchDateUtc)
                .Take(100)
                .ToListAsync();

            _logger.LogInformation("GetMatches returning {Count} results", results.Count);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await response.WriteAsJsonAsync(results);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatches: {Message}", ex.Message);
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            errorResponse.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            errorResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message, stackTrace = ex.StackTrace });
            return errorResponse;
        }
    }

    /// <summary>
    /// Parse season parameter to date range.
    /// </summary>
    private (DateTime? StartDate, DateTime? EndDate) ParseSeason(string? season)
    {
        return season?.ToLower() switch
        {
            "fall2025" => (new DateTime(2025, 7, 1), new DateTime(2025, 12, 31, 23, 59, 59)),
            "spring2026" => (new DateTime(2026, 1, 1), new DateTime(2026, 6, 30, 23, 59, 59)),
            _ => (null, null)
        };
    }
}
