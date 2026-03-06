using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using YSS.Data;
using Microsoft.EntityFrameworkCore;
using System.Web;

namespace YSS.Functions.Triggers;

public class GetStandings
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public GetStandings(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<GetStandings>();
    }

    [Function("GetStandings")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "standings")] HttpRequestData req)
    {
        try
        {
            var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
            var program = queryParams["program"] ?? string.Empty;
            var season = queryParams["season"] ?? string.Empty;
            var region = queryParams["region"] ?? string.Empty;
            var ageGroup = queryParams["ageGroup"] ?? string.Empty;

            _logger.LogInformation("GetStandings called with: program={Program}, season={Season}, region={Region}, ageGroup={AgeGroup}",
                program, season, region, ageGroup);

            // program, region, ageGroup are required; season defaults to full 2025-2026 if omitted
            if (string.IsNullOrEmpty(program) || string.IsNullOrEmpty(region) || string.IsNullOrEmpty(ageGroup))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                await errorResponse.WriteAsJsonAsync(new { error = "Missing required parameters: program, region, ageGroup" });
                return errorResponse;
            }

            // Default to full season if not specified
            if (string.IsNullOrEmpty(season))
                season = "2025-2026";

            var matches = _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Region)
                .ThenInclude(r => r.Division)
                .Include(m => m.AgeGroup)
                .AsQueryable();

            // Filter by program (homegrown=12, academy=35)
            int? tournamentId = program.ToLower() switch
            {
                "homegrown" => 12,
                "academy" => 35,
                _ => null
            };

            if (tournamentId.HasValue)
            {
                matches = matches.Where(m => m.Region.Division.TournamentId == tournamentId.Value);
            }

            // Filter by season
            var (seasonStartDate, seasonEndDate) = ParseSeason(season);
            if (seasonStartDate.HasValue)
                matches = matches.Where(m => m.MatchDateUtc >= seasonStartDate.Value);
            if (seasonEndDate.HasValue)
                matches = matches.Where(m => m.MatchDateUtc <= seasonEndDate.Value);

            // Filter by region
            matches = matches.Where(m => m.Region.Name == region);

            // Filter by age group
            matches = matches.Where(m => m.AgeGroup.Name == ageGroup);

            // Get all matches for this filter set
            var allMatches = await matches.ToListAsync();
            _logger.LogInformation("Found {Count} total matches for standings", allMatches.Count);

            // Compute standings from scored matches only
            var standings = ComputeStandings(allMatches);
            _logger.LogInformation("Computed standings for {Count} teams", standings.Count);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await response.WriteAsJsonAsync(standings);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetStandings: {Message}", ex.Message);
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    private (DateTime? StartDate, DateTime? EndDate) ParseSeason(string season)
    {
        return season.ToLower() switch
        {
            "2025-2026" => (new DateTime(2025, 7, 1), new DateTime(2026, 6, 30, 23, 59, 59)),
            "fall2025"  => (new DateTime(2025, 7, 1), new DateTime(2025, 12, 31, 23, 59, 59)),
            "spring2026"=> (new DateTime(2026, 1, 1), new DateTime(2026, 6, 30, 23, 59, 59)),
            _ => ((DateTime?)null, (DateTime?)null)
        };
    }

    private List<StandingRowDto> ComputeStandings(List<YSS.Data.Entities.Match> matches)
    {
        var teamStats = new Dictionary<string, TeamStats>();

        foreach (var match in matches)
        {
            // Skip matches without scores
            if (string.IsNullOrEmpty(match.Score) || match.Score == "TBD")
                continue;

            // Parse score
            var (homeGoals, awayGoals) = ParseScore(match.Score);
            if (!homeGoals.HasValue || !awayGoals.HasValue)
                continue;

            var homeTeamName = match.HomeTeam.Name;
            var awayTeamName = match.AwayTeam.Name;

            // Initialize team stats if not seen before
            if (!teamStats.ContainsKey(homeTeamName))
                teamStats[homeTeamName] = new TeamStats { TeamId = match.HomeTeam.Id, TeamName = homeTeamName, LogoUrl = match.HomeTeam.LogoUrl };
            if (!teamStats.ContainsKey(awayTeamName))
                teamStats[awayTeamName] = new TeamStats { TeamId = match.AwayTeam.Id, TeamName = awayTeamName, LogoUrl = match.AwayTeam.LogoUrl };

            // Update home team stats
            var homeStats = teamStats[homeTeamName];
            homeStats.GP++;
            homeStats.GF += homeGoals.Value;
            homeStats.GA += awayGoals.Value;
            homeStats.HomeGF += homeGoals.Value;
            homeStats.HomeGA += awayGoals.Value;

            if (homeGoals > awayGoals)
                homeStats.W++;
            else if (homeGoals == awayGoals)
                homeStats.D++;
            else
                homeStats.L++;

            // Update away team stats
            var awayStats = teamStats[awayTeamName];
            awayStats.GP++;
            awayStats.GF += awayGoals.Value;
            awayStats.GA += homeGoals.Value;
            awayStats.AwayGF += awayGoals.Value;
            awayStats.AwayGA += homeGoals.Value;

            if (awayGoals > homeGoals)
                awayStats.W++;
            else if (awayGoals == homeGoals)
                awayStats.D++;
            else
                awayStats.L++;
        }

        // Convert to standings rows with rank
        // Tiebreaker order (MLS Next): PPM → wins → GD → GF → away GD → away GF → home GD → home GF
        // Note: head-to-head (2-team ties only) is not implemented — requires custom pairwise sort
        var rows = teamStats.Values
            .Select(ts => new StandingRowDto
            {
                TeamId = ts.TeamId,
                TeamName = ts.TeamName,
                LogoUrl = ts.LogoUrl,
                GP = ts.GP,
                W = ts.W,
                D = ts.D,
                L = ts.L,
                GF = ts.GF,
                GA = ts.GA,
                GD = ts.GF - ts.GA,
                Pts = ts.W * 3 + ts.D,
                PPM = ts.GP > 0 ? Math.Round((decimal)(ts.W * 3 + ts.D) / ts.GP, 3) : 0m,
                GFM = ts.GP > 0 ? Math.Round((decimal)ts.GF / ts.GP, 2) : 0m,
                GAM = ts.GP > 0 ? Math.Round((decimal)ts.GA / ts.GP, 2) : 0m,
                GDM = ts.GP > 0 ? Math.Round((decimal)(ts.GF - ts.GA) / ts.GP, 2) : 0m,
                AwayGD = ts.AwayGF - ts.AwayGA,
                AwayGF = ts.AwayGF,
                HomeGD = ts.HomeGF - ts.HomeGA,
                HomeGF = ts.HomeGF
            })
            .OrderByDescending(r => r.PPM)
            .ThenByDescending(r => r.W)
            .ThenByDescending(r => r.GD)
            .ThenByDescending(r => r.GF)
            .ThenByDescending(r => r.AwayGD)
            .ThenByDescending(r => r.AwayGF)
            .ThenByDescending(r => r.HomeGD)
            .ThenByDescending(r => r.HomeGF)
            .ToList();

        // Add rank
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].Rank = i + 1;
        }

        return rows;
    }

    private (int? homeGoals, int? awayGoals) ParseScore(string score)
    {
        if (string.IsNullOrEmpty(score))
            return (null, null);

        // Remove parenthetical notation like "(PK)" or "AET"
        var cleanScore = score.Split('(')[0].Trim();
        var parts = cleanScore.Split('-');

        if (parts.Length < 2)
            return (null, null);

        var homePart = parts[0].Trim();
        var awayPart = parts[1].Trim();

        // Extract just the number from away part if it has extra text
        if (awayPart.Contains(' '))
            awayPart = awayPart.Split(' ')[0].Trim();

        if (int.TryParse(homePart, out int homeGoals) && int.TryParse(awayPart, out int awayGoals))
            return (homeGoals, awayGoals);

        return (null, null);
    }

    private class TeamStats
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public int GP { get; set; }
        public int W { get; set; }
        public int D { get; set; }
        public int L { get; set; }
        public int GF { get; set; }
        public int GA { get; set; }
        public int HomeGF { get; set; }
        public int HomeGA { get; set; }
        public int AwayGF { get; set; }
        public int AwayGA { get; set; }
    }

    public class StandingRowDto
    {
        public int Rank { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public int GP { get; set; }
        public int W { get; set; }
        public int D { get; set; }
        public int L { get; set; }
        public int GF { get; set; }
        public int GA { get; set; }
        public int GD { get; set; }
        public int Pts { get; set; }
        public decimal PPM { get; set; }
        public decimal GFM { get; set; }
        public decimal GAM { get; set; }
        public decimal GDM { get; set; }
        public int AwayGD { get; set; }
        public int AwayGF { get; set; }
        public int HomeGD { get; set; }
        public int HomeGF { get; set; }
    }
}
