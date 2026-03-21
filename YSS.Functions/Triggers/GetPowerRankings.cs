using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using YSS.Data;
using YSS.Functions.Services;
using Microsoft.EntityFrameworkCore;
using System.Web;

namespace YSS.Functions.Triggers;

public class GetPowerRankings
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public GetPowerRankings(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<GetPowerRankings>();
    }

    [Function("GetPowerRankings")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "powerrankings")] HttpRequestData req)
    {
        try
        {
            var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
            var program  = queryParams["program"]  ?? string.Empty;
            var ageGroup = queryParams["ageGroup"] ?? string.Empty;

            _logger.LogInformation("GetPowerRankings called: program={Program}, ageGroup={AgeGroup}",
                program, ageGroup);

            if (string.IsNullOrEmpty(program) || string.IsNullOrEmpty(ageGroup))
                return await BadRequest(req, "Missing required parameters: program, ageGroup");

            var tournamentIds = program.ToLower() switch
            {
                "homegrown" => new[] { 12, 75 },
                "academy"   => new[] { 35 },
                _           => Array.Empty<int>()
            };

            if (tournamentIds.Length == 0)
                return await BadRequest(req, "Invalid program. Use 'homegrown' or 'academy'");

            var oneYearAgo = DateTime.UtcNow.AddYears(-1);

            var matches = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.AgeGroup)
                .Include(m => m.Region)
                    .ThenInclude(r => r.Division)
                .Where(m =>
                    tournamentIds.Contains(m.Region.Division.TournamentId) &&
                    m.AgeGroup.Name == ageGroup &&
                    m.Score != null && m.Score != "" && m.Score != "TBD" &&
                    m.MatchDateUtc >= oneYearAgo)
                .OrderBy(m => m.MatchDateUtc)
                .ToListAsync();

            _logger.LogInformation("GetPowerRankings: {Count} completed matches found", matches.Count);

            // Build ELO inputs and team info lookup
            var eloInputs = new List<EloCalculator.EloMatchInput>();
            var teamInfo = new Dictionary<int, TeamInfo>();

            foreach (var match in matches)
            {
                if (!TryParseScore(match.Score!, out var homeScore, out var awayScore))
                    continue;

                eloInputs.Add(new EloCalculator.EloMatchInput(
                    match.HomeTeamId, match.AwayTeamId, homeScore, awayScore, match.MatchDateUtc));

                if (!teamInfo.ContainsKey(match.HomeTeamId))
                    teamInfo[match.HomeTeamId] = new TeamInfo(match.HomeTeam.Name, match.HomeTeam.LogoUrl, match.Region.Name);
                teamInfo[match.HomeTeamId].RegionNames.Add(match.Region.Name);

                if (!teamInfo.ContainsKey(match.AwayTeamId))
                    teamInfo[match.AwayTeamId] = new TeamInfo(match.AwayTeam.Name, match.AwayTeam.LogoUrl, match.Region.Name);
                teamInfo[match.AwayTeamId].RegionNames.Add(match.Region.Name);
            }

            var eloResults = EloCalculator.ComputeRankings(eloInputs);

            var rankings = eloResults
                .Where(kvp => kvp.Value.GP >= 3)
                .OrderByDescending(kvp => kvp.Value.Rating)
                .Select((kvp, idx) =>
                {
                    var info = teamInfo.GetValueOrDefault(kvp.Key);
                    var state = kvp.Value;
                    return new PowerRankingDto
                    {
                        Rank        = idx + 1,
                        TeamName    = info?.Name ?? "Unknown",
                        LogoUrl     = info?.LogoUrl,
                        RegionName  = info?.PrimaryRegion ?? "",
                        RegionNames = info?.RegionNames.OrderBy(x => x).ToList() ?? new List<string>(),
                        EloRating   = (int)Math.Round(state.Rating),
                        EloDelta    = Math.Round(state.RecentDeltas.Sum(), 1),
                        GP          = state.GP
                    };
                })
                .ToList();

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await response.WriteAsJsonAsync(rankings);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPowerRankings: {Message}", ex.Message);
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    private static bool TryParseScore(string score, out int homeScore, out int awayScore)
    {
        homeScore = 0;
        awayScore = 0;
        if (string.IsNullOrWhiteSpace(score)) return false;
        var parts = score.Split('-');
        if (parts.Length != 2) return false;
        return int.TryParse(parts[0].Trim(), out homeScore) &&
               int.TryParse(parts[1].Trim(), out awayScore);
    }

    private async Task<HttpResponseData> BadRequest(HttpRequestData req, string message)
    {
        var r = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        r.Headers.Add("Access-Control-Allow-Origin", "*");
        await r.WriteAsJsonAsync(new { error = message });
        return r;
    }

    private record TeamInfo(string Name, string? LogoUrl, string PrimaryRegion)
    {
        public HashSet<string> RegionNames { get; } = new();
    }

    public class PowerRankingDto
    {
        public int Rank { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string RegionName { get; set; } = string.Empty;
        public List<string> RegionNames { get; set; } = new();
        public int EloRating { get; set; }
        public double EloDelta { get; set; }
        public int GP { get; set; }
    }
}
