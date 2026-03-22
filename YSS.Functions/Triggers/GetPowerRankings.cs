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

            var isHomegrown = program.ToLower() == "homegrown";
            var isAcademy = program.ToLower() == "academy";

            if (!isHomegrown && !isAcademy)
                return await BadRequest(req, "Invalid program. Use 'homegrown' or 'academy'");

            var oneYearAgo = DateTime.UtcNow.AddYears(-1);

            // Load matches to build per-team metadata (GP, regions, recent deltas)
            var matches = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.AgeGroup)
                .Include(m => m.Region)
                    .ThenInclude(r => r.Division)
                .Include(m => m.Competition)
                .Where(m =>
                    (isAcademy
                        ? (m.Region.Division.TournamentId == 35 || m.Competition.Name.StartsWith("AD"))
                        : (new[] { 12, 75 }.Contains(m.Region.Division.TournamentId) && !m.Competition.Name.StartsWith("AD"))) &&
                    m.AgeGroup.Name == ageGroup &&
                    m.Score != null && m.Score != "" && m.Score != "TBD" &&
                    m.MatchDateUtc >= oneYearAgo)
                .OrderBy(m => m.MatchDateUtc)
                .ToListAsync();

            _logger.LogInformation("GetPowerRankings: {Count} completed matches found", matches.Count);

            // Build per-team metadata (GP, regions) and compute deltas from recent matches
            var teamInfo = new Dictionary<int, TeamInfo>();
            // Track recent match results for delta calculation (last 5 matches per team)
            var teamRecentMatches = new Dictionary<int, List<(DateTime Date, int HomeTeamId, int AwayTeamId, int HomeScore, int AwayScore)>>();

            foreach (var match in matches)
            {
                if (!TryParseScore(match.Score!, out var homeScore, out var awayScore))
                    continue;

                if (!teamInfo.ContainsKey(match.HomeTeamId))
                    teamInfo[match.HomeTeamId] = new TeamInfo(match.HomeTeam.Name, match.HomeTeam.LogoUrl, match.Region.Name);
                teamInfo[match.HomeTeamId].RegionNames.Add(match.Region.Name);
                teamInfo[match.HomeTeamId].GP++;

                if (!teamInfo.ContainsKey(match.AwayTeamId))
                    teamInfo[match.AwayTeamId] = new TeamInfo(match.AwayTeam.Name, match.AwayTeam.LogoUrl, match.Region.Name);
                teamInfo[match.AwayTeamId].RegionNames.Add(match.Region.Name);
                teamInfo[match.AwayTeamId].GP++;

                var entry = (match.MatchDateUtc, match.HomeTeamId, match.AwayTeamId, homeScore, awayScore);

                if (!teamRecentMatches.ContainsKey(match.HomeTeamId))
                    teamRecentMatches[match.HomeTeamId] = new();
                teamRecentMatches[match.HomeTeamId].Add(entry);

                if (!teamRecentMatches.ContainsKey(match.AwayTeamId))
                    teamRecentMatches[match.AwayTeamId] = new();
                teamRecentMatches[match.AwayTeamId].Add(entry);
            }

            // Compute recent ELO deltas by replaying last 5 matches per team using EloCalculator
            // We use the full match set to compute accurate ELO, then take delta from last 5
            var eloInputs = new List<EloCalculator.EloMatchInput>();
            foreach (var match in matches)
            {
                if (!TryParseScore(match.Score!, out var hs, out var aws)) continue;
                eloInputs.Add(new EloCalculator.EloMatchInput(match.HomeTeamId, match.AwayTeamId, hs, aws, match.MatchDateUtc));
            }
            var eloResults = EloCalculator.ComputeRankings(eloInputs);

            // Use stored Team.EloRating as the authoritative rating, with on-the-fly deltas
            var rankings = teamInfo
                .Where(kvp => kvp.Value.GP >= 3)
                .Select(kvp =>
                {
                    var teamId = kvp.Key;
                    var info = kvp.Value;
                    // Read stored ELO from the team entity
                    var storedElo = matches
                        .Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId)
                        .Select(m => m.HomeTeamId == teamId ? m.HomeTeam.EloRating : m.AwayTeam.EloRating)
                        .FirstOrDefault();
                    var delta = eloResults.TryGetValue(teamId, out var state)
                        ? Math.Round(state.RecentDeltas.Sum(), 1)
                        : 0.0;
                    return new PowerRankingDto
                    {
                        Rank        = 0, // assigned after sorting
                        TeamName    = info.Name,
                        LogoUrl     = info.LogoUrl,
                        RegionName  = info.PrimaryRegion,
                        RegionNames = info.RegionNames.OrderBy(x => x).ToList(),
                        EloRating   = storedElo,
                        EloDelta    = delta,
                        GP          = info.GP
                    };
                })
                .OrderByDescending(r => r.EloRating)
                .ToList();

            // Assign ranks
            for (int i = 0; i < rankings.Count; i++)
                rankings[i].Rank = i + 1;

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
        public int GP { get; set; }
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
