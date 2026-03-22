using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using YSS.Data;
using Microsoft.EntityFrameworkCore;
using System.Web;

namespace YSS.Functions.Triggers;

public class GetAnalytics
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public GetAnalytics(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<GetAnalytics>();
    }

    [Function("GetAnalytics")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "analytics")] HttpRequestData req)
    {
        try
        {
            var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
            var program  = queryParams["program"]  ?? string.Empty;
            var ageGroup = queryParams["ageGroup"] ?? string.Empty;
            var region   = queryParams["region"]   ?? string.Empty;

            _logger.LogInformation("GetAnalytics called: program={Program}, ageGroup={AgeGroup}, region={Region}",
                program, ageGroup, region);

            if (string.IsNullOrEmpty(program) || string.IsNullOrEmpty(ageGroup))
                return await BadRequest(req, "Missing required parameters: program, ageGroup");

            var isHomegrown = program.ToLower() == "homegrown";
            var isAcademy = program.ToLower() == "academy";

            if (!isHomegrown && !isAcademy)
                return await BadRequest(req, "Invalid program. Use 'homegrown' or 'academy'");

            var tournamentIds = isHomegrown ? new[] { 12 } : new[] { 35 };
            var festCompetitions = isHomegrown
                ? new[] { "HD Showcase", "Pro Player Pathway", "Best Of" }
                : new[] { "AD Showcase" };

            // Load completed matches for this program + age group (include FEST split by competition)
            var query = _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.AgeGroup)
                .Include(m => m.Region)
                    .ThenInclude(r => r.Division)
                .Include(m => m.Competition)
                .Where(m =>
                    (tournamentIds.Contains(m.Region.Division.TournamentId) ||
                     (m.Region.Division.TournamentId == 75 && festCompetitions.Contains(m.Competition.Name))) &&
                    m.AgeGroup.Name == ageGroup &&
                    m.Score != null && m.Score != "" && m.Score != "TBD");

            if (!string.IsNullOrEmpty(region))
                query = query.Where(m => m.Region.Name == region);

            var matches = await query.ToListAsync();

            _logger.LogInformation("GetAnalytics: {Count} completed matches found", matches.Count);

            // Build per-team result history
            var teamData = new Dictionary<int, TeamRecord>();

            foreach (var match in matches)
            {
                if (!TryParseScore(match.Score!, out var homeScore, out var awayScore))
                    continue;

                var homeResult = homeScore > awayScore ? "W" : homeScore < awayScore ? "L" : "D";
                var awayResult = awayScore > homeScore ? "W" : awayScore < homeScore ? "L" : "D";

                if (!teamData.ContainsKey(match.HomeTeamId))
                    teamData[match.HomeTeamId] = new TeamRecord(match.HomeTeam.Name, match.HomeTeam.LogoUrl, match.Region.Name);
                teamData[match.HomeTeamId].Results.Add((match.MatchDateUtc, homeResult));
                teamData[match.HomeTeamId].Opponents.Add(match.AwayTeamId);
                teamData[match.HomeTeamId].RegionNames.Add(match.Region.Name);

                if (!teamData.ContainsKey(match.AwayTeamId))
                    teamData[match.AwayTeamId] = new TeamRecord(match.AwayTeam.Name, match.AwayTeam.LogoUrl, match.Region.Name);
                teamData[match.AwayTeamId].Results.Add((match.MatchDateUtc, awayResult));
                teamData[match.AwayTeamId].Opponents.Add(match.HomeTeamId);
                teamData[match.AwayTeamId].RegionNames.Add(match.Region.Name);
            }

            var teamPpg = teamData.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Results.Count == 0 ? 0.0 :
                       kvp.Value.Results.Sum(r => r.Result == "W" ? 3 : r.Result == "D" ? 1 : 0)
                       / (double)kvp.Value.Results.Count);

            var result = teamData
                .Select(kvp =>
                {
                    var (teamId, rec) = (kvp.Key, kvp.Value);
                    var sorted    = rec.Results.OrderByDescending(r => r.Date).ToList();
                    var last8     = sorted.Take(8).Select(r => r.Result).ToList();
                    var rawScore  = ComputeMomentum(last8);
                    // Bayesian shrinkage: pull toward 50 when sample is small
                    var gp        = rec.Results.Count;
                    var score     = (rawScore * gp + 50.0 * 5) / (gp + 5);
                    var sos    = rec.Opponents.Count == 0 ? 0.0
                        : rec.Opponents
                            .Where(id => teamPpg.ContainsKey(id))
                            .Select(id => teamPpg[id])
                            .DefaultIfEmpty(0)
                            .Average();
                    return new TeamAnalyticsDto
                    {
                        TeamName      = rec.Name,
                        LogoUrl       = rec.LogoUrl,
                        RegionName    = rec.RegionName,
                        RegionNames   = rec.RegionNames.OrderBy(x => x).ToList(),
                        GP            = rec.Results.Count,
                        Last8         = last8,
                        MomentumScore = Math.Round(score, 1),
                        MomentumLabel = GetMomentumLabel(score),
                        Sos           = Math.Round(sos, 2),
                    };
                })
                .OrderByDescending(t => t.MomentumScore)
                .ToList();

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAnalytics: {Message}", ex.Message);
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

    private static double ComputeMomentum(List<string> last8)
    {
        // last8[0] = most recent; recent 3 emphasized, oldest 3 deemphasized
        // weights: [8, 7, 6, 4, 3, 2, 1, 1]
        if (last8.Count == 0) return 0;
        int[] weights = [8, 7, 6, 4, 3, 2, 1, 1];
        double weightedSum = 0;
        double weightSum   = 0;
        for (int i = 0; i < last8.Count; i++)
        {
            var points = last8[i] == "W" ? 3 : last8[i] == "D" ? 1 : 0;
            weightedSum += points * weights[i];
            weightSum   += weights[i];
        }
        // Normalize: max weighted average = 3 (all wins), map to 0–100
        return (weightedSum / weightSum) / 3.0 * 100.0;
    }

    private static string GetMomentumLabel(double score) => score switch
    {
        >= 80 => "On Fire 🔥",
        >= 60 => "Strong Form 💪",
        >= 40 => "Neutral ➡️",
        >= 20 => "Slumping 📉",
        _     => "Ice Cold 🧊"
    };

    private async Task<HttpResponseData> BadRequest(HttpRequestData req, string message)
    {
        var r = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        r.Headers.Add("Access-Control-Allow-Origin", "*");
        await r.WriteAsJsonAsync(new { error = message });
        return r;
    }

    private record TeamRecord(string Name, string? LogoUrl, string RegionName)
    {
        public List<(DateTime Date, string Result)> Results { get; } = new();
        public List<int> Opponents { get; } = new();
        public HashSet<string> RegionNames { get; } = new();
    }

    public class TeamAnalyticsDto
    {
        public string TeamName          { get; set; } = string.Empty;
        public string? LogoUrl          { get; set; }
        public string RegionName        { get; set; } = string.Empty;
        public List<string> RegionNames { get; set; } = new();
        public double MomentumScore     { get; set; }
        public string MomentumLabel     { get; set; } = string.Empty;
        public List<string> Last8       { get; set; } = new();
        public int GP                   { get; set; }
        public double Sos               { get; set; }
    }
}
