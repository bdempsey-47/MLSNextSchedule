using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using YSS.Data;
using YSS.Functions.Services;
using Microsoft.EntityFrameworkCore;

namespace YSS.Functions.Triggers;

public class GetHomepageStats
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public GetHomepageStats(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<GetHomepageStats>();
    }

    [Function("GetHomepageStats")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "homepagestats")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("GetHomepageStats called");

            // Read pre-computed snapshot from database
            var snapshot = await _context.HomepageSnapshots.FirstOrDefaultAsync();

            if (snapshot == null)
            {
                _logger.LogWarning("Homepage snapshot not found, returning empty response");
                var emptyResult = new HomepageStatsDto();

                var emptyResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                emptyResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                emptyResponse.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
                emptyResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                emptyResponse.Headers.Add("Cache-Control", "public, max-age=3600");
                await emptyResponse.WriteAsJsonAsync(emptyResult);
                return emptyResponse;
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            response.Headers.Add("Cache-Control", "public, max-age=3600");
            response.Headers.Add("Content-Type", "application/json");

            // Directly write the pre-computed JSON (already serialized)
            await response.WriteStringAsync(snapshot.StatsJson);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetHomepageStats: {Message}", ex.Message);
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    // === DTOs ===
    public class HomepageStatsDto
    {
        public Dictionary<string, List<MiniRankingDto>> AcademyTopElo { get; set; } = new();
        public Dictionary<string, List<MiniRankingDto>> HomegrownTopElo { get; set; } = new();
        public List<RegionDominanceDto> FestHomegrownRegions { get; set; } = new();
        public List<RegionDominanceDto> FestAcademyRegions { get; set; } = new();
        public Dictionary<string, UpsetDto> BiggestUpsets { get; set; } = new();
        public Dictionary<string, MatchOfWeekDto> MatchesOfTheWeek { get; set; } = new();
        public QuickStatsDto QuickStats { get; set; } = new();
    }

    public class MiniRankingDto
    {
        public int Rank { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string RegionName { get; set; } = string.Empty;
        public int EloRating { get; set; }
        public double EloDelta { get; set; }
    }

    public class RegionDominanceDto
    {
        public int Rank { get; set; }
        public string RegionName { get; set; } = string.Empty;
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference { get; set; }
    }

    public class UpsetDto
    {
        public string HomeTeamName { get; set; } = string.Empty;
        public string? HomeLogoUrl { get; set; }
        public int HomeElo { get; set; }
        public string AwayTeamName { get; set; } = string.Empty;
        public string? AwayLogoUrl { get; set; }
        public int AwayElo { get; set; }
        public string Score { get; set; } = string.Empty;
        public bool HomeWon { get; set; }
        public int EloDiff { get; set; }
        public string MatchDate { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
    }

    public class MatchOfWeekDto
    {
        public string HomeTeamName { get; set; } = string.Empty;
        public string? HomeLogoUrl { get; set; }
        public int HomeElo { get; set; }
        public string AwayTeamName { get; set; } = string.Empty;
        public string? AwayLogoUrl { get; set; }
        public int AwayElo { get; set; }
        public string MatchDate { get; set; } = string.Empty;
        public int CombinedElo { get; set; }
        public string Program { get; set; } = string.Empty;
    }

    public class QuickStatsDto
    {
        public int TotalMatches { get; set; }
        public int TotalTeams { get; set; }
        public int TotalRegions { get; set; }
        public int CompletedMatches { get; set; }
    }
}
