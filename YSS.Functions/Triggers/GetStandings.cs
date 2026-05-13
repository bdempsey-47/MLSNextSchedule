using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Web;
using YSS.Data;
using Microsoft.EntityFrameworkCore;
using YSS.Constants;

namespace YSS.Functions.Triggers;

public class GetStandings
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    // Maps our age group display names to Modular11's UID_age parameter
    private static readonly Dictionary<string, string> AgeGroupMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["U13"]     = "21",
        ["U14"]     = "22",
        ["U15"]     = "33",
        ["U16"]     = "14",
        ["U17"]     = "15",
        ["U18/19"]  = "26",
        ["U18/U19"] = "26",
        ["U19"]     = "26",
    };

    // Captures everything between "U{age} " and " Division"
    // e.g. "Male -  U17 Northeast (Pro Player Pathway) Division" → "Northeast (Pro Player Pathway)"
    // e.g. "Male -  U17 Northeast Division"                      → "Northeast"
    private static readonly Regex RegionNameRegex =
        new(@"U\d+\s+(.+?)\s+Division", RegexOptions.Compiled);

    public GetStandings(IHttpClientFactory httpClientFactory, AppDbContext context, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
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
            var program  = queryParams["program"]  ?? string.Empty;
            var ageGroup = queryParams["ageGroup"] ?? string.Empty;

            _logger.LogInformation("GetStandings called: program={Program}, ageGroup={AgeGroup}", program, ageGroup);

            if (string.IsNullOrEmpty(program) || string.IsNullOrEmpty(ageGroup))
                return await BadRequest(req, "Missing required parameters: program, ageGroup");

            // Program-specific Modular11 parameters
            var (uidEvent, uidGender, listType, referer) = program.ToLower() switch
            {
                "homegrown" => (TournamentConstants.HomegrownTournamentId, 1, 53, "https://www.modular11.com/standings?year=21&gender=1"),
                "academy"   => (TournamentConstants.AcademyTournamentId, 3, 71, "https://www.modular11.com/league-standings/mls-next-academy-division/21"),
                _           => (0,  0,  0,  "")
            };
            if (uidEvent == 0)
                return await BadRequest(req, "Invalid program. Use 'homegrown' or 'academy'");

            if (!AgeGroupMap.TryGetValue(ageGroup, out var uidAge))
                return await BadRequest(req, $"Unknown age group: {ageGroup}");

            var url = $"https://www.modular11.com/public_schedule/league/get_teams" +
                      $"?tournament_type=league&UID_event={uidEvent}&UID_age={uidAge}&UID_gender={uidGender}&list_type={listType}";

            _logger.LogInformation("Fetching Modular11 standings: {Url}", url);

            var client = _httpClientFactory.CreateClient("standings");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("accept", "text/html, */*; q=0.01");
            request.Headers.Add("x-requested-with", "XMLHttpRequest");
            request.Headers.Add("referer", referer);
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");

            var httpResponse = await client.SendAsync(request);
            httpResponse.EnsureSuccessStatusCode();
            var html = await httpResponse.Content.ReadAsStringAsync();

            _logger.LogInformation("Modular11 response size: {Length} chars", html.Length);

            var isQoP = html.Contains("Quality of Play");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (isQoP)
            {
                var rankings = await ParseQoPRankingsAsync(html);
                _logger.LogInformation("Parsed {Count} QoP rankings", rankings.Count);
                await response.WriteAsJsonAsync(new { Type = "qop", Rankings = rankings });
            }
            else
            {
                var groups = await ParseStandingsAsync(html);
                await ComputeSorsAsync(groups, program, ageGroup);
                _logger.LogInformation("Parsed {Count} standing groups", groups.Count);
                await response.WriteAsJsonAsync(new { Type = "standings", Groups = groups });
            }

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

    private async Task<HttpResponseData> BadRequest(HttpRequestData req, string message)
    {
        var r = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        r.Headers.Add("Access-Control-Allow-Origin", "*");
        await r.WriteAsJsonAsync(new { error = message });
        return r;
    }

    private async Task<List<StandingsGroupDto>> ParseStandingsAsync(string html)
    {
        var context  = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html));

        // Extract division heading names in DOM order → ["Central", "West", ...]
        var headingElements = document.QuerySelectorAll(".container-group-text p[data-title]");
        var regionNames = headingElements
            .Select(el => ExtractRegionName(el.GetAttribute("data-title") ?? ""))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        // Group team rows by js-group, preserving first-occurrence order
        var teamRows = document.QuerySelectorAll(".form_row.main_row[js-group]");
        var groupOrder = new List<string>();
        var rowsByGroup = new Dictionary<string, List<IElement>>();

        foreach (var row in teamRows)
        {
            var groupId = row.GetAttribute("js-group") ?? "";
            if (!rowsByGroup.ContainsKey(groupId))
            {
                rowsByGroup[groupId] = new List<IElement>();
                groupOrder.Add(groupId);
            }
            rowsByGroup[groupId].Add(row);
        }

        // Match region name to group ID by position
        var result = new List<StandingsGroupDto>();
        for (int i = 0; i < groupOrder.Count; i++)
        {
            var groupId    = groupOrder[i];
            var regionName = i < regionNames.Count ? regionNames[i] : groupId;
            var standings  = rowsByGroup[groupId]
                .Select(ParseTeamRow)
                .Where(r => r is not null)
                .Cast<StandingRowDto>()
                .ToList();

            result.Add(new StandingsGroupDto { RegionName = regionName, Standings = standings });
        }

        return result;
    }

    private string ExtractRegionName(string title)
    {
        // "Male -  U17 Central (Pro Player Pathway) Division" → "Central"
        var match = RegionNameRegex.Match(title);
        return match.Success ? match.Groups[1].Value.Trim() : title;
    }

    private StandingRowDto? ParseTeamRow(IElement row)
    {
        try
        {
            var rank     = int.TryParse(row.QuerySelector(".container-rank")?.TextContent?.Trim(), out var r) ? r : 0;
            var teamName = row.QuerySelector("p[data-title]")?.GetAttribute("data-title")?.Trim() ?? "";
            var logoUrl  = row.QuerySelector(".container-img img")?.GetAttribute("src");

            if (string.IsNullOrEmpty(teamName)) return null;

            // Mobile cells: PTS (index 0), PPM (index 1)
            var mobileCells = row.QuerySelectorAll(".col-xs-6.col-sm-1.pad-0.gap-right-mobile-lg").ToList();
            var pts = mobileCells.Count > 0 && int.TryParse(mobileCells[0].TextContent?.Trim(), out var p) ? p : 0;
            var ppm = mobileCells.Count > 1 && decimal.TryParse(
                mobileCells[1].TextContent?.Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var ppmVal) ? ppmVal : 0m;

            // Desktop hidden cells: MP (0), W (1), L (2), T/draw (3), GF (4), GA (5)
            var desktopCells = row.QuerySelectorAll(".col-sm-1.pad-0.gap-right-mobile-sm.hidden-xs").ToList();
            var gp = desktopCells.Count > 0 && int.TryParse(desktopCells[0].TextContent?.Trim(), out var gp_) ? gp_ : 0;
            var w  = desktopCells.Count > 1 && int.TryParse(desktopCells[1].TextContent?.Trim(), out var w_)  ? w_  : 0;
            var l  = desktopCells.Count > 2 && int.TryParse(desktopCells[2].TextContent?.Trim(), out var l_)  ? l_  : 0;
            var d  = desktopCells.Count > 3 && int.TryParse(desktopCells[3].TextContent?.Trim(), out var d_)  ? d_  : 0;
            var gf = desktopCells.Count > 4 && decimal.TryParse(desktopCells[4].TextContent?.Trim(),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gf_) ? gf_ : 0m;
            var ga = desktopCells.Count > 5 && decimal.TryParse(desktopCells[5].TextContent?.Trim(),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ga_) ? ga_ : 0m;

            var wpm  = gp > 0 ? Math.Round((decimal)w  / gp, 3) : 0m;
            var gdpm = gp > 0 ? Math.Round((gf - ga) / gp, 3) : 0m;
            var gpm  = gp > 0 ? Math.Round(gf        / gp, 3) : 0m;

            return new StandingRowDto
            {
                Rank     = rank,
                TeamName = teamName,
                LogoUrl  = logoUrl,
                GP       = gp,
                W        = w,
                D        = d,
                L        = l,
                GF       = gf,
                GA       = ga,
                GD       = gf - ga,
                Pts      = pts,
                PPM      = ppm,
                WPM      = wpm,
                GDPM     = gdpm,
                GPM      = gpm,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse a team row");
            return null;
        }
    }

    private async Task<List<QoPRankingDto>> ParseQoPRankingsAsync(string html)
    {
        var context  = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html));

        // Extract division headings in DOM order for group → division mapping
        var headingElements = document.QuerySelectorAll(".container-group-text p[data-title]");
        var divisionNames = headingElements
            .Select(el => el.GetAttribute("data-title")?.Trim() ?? "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        // Group team rows by js-group, preserving first-occurrence order
        var teamRows = document.QuerySelectorAll(".form_row.main_row[js-group]");
        var groupOrder = new List<string>();
        var rowsByGroup = new Dictionary<string, List<IElement>>();

        foreach (var row in teamRows)
        {
            var groupId = row.GetAttribute("js-group") ?? "";
            if (!rowsByGroup.ContainsKey(groupId))
            {
                rowsByGroup[groupId] = new List<IElement>();
                groupOrder.Add(groupId);
            }
            rowsByGroup[groupId].Add(row);
        }

        var result = new List<QoPRankingDto>();
        var globalRank = 1;

        for (int i = 0; i < groupOrder.Count; i++)
        {
            var groupId = groupOrder[i];
            var divisionName = i < divisionNames.Count ? divisionNames[i] : "";

            foreach (var row in rowsByGroup[groupId])
            {
                var dto = ParseQoPRow(row, divisionName, globalRank);
                if (dto != null)
                {
                    result.Add(dto);
                    globalRank++;
                }
            }
        }

        // Sort by QoP descending for a cross-region national ranking
        result = result.OrderByDescending(r => r.QualityOfPlay).ToList();
        for (int i = 0; i < result.Count; i++)
            result[i].Rank = i + 1;

        return result;
    }

    private QoPRankingDto? ParseQoPRow(IElement row, string divisionName, int rank)
    {
        try
        {
            var teamName = row.QuerySelector("p[data-title]")?.GetAttribute("data-title")?.Trim() ?? "";
            var logoUrl  = row.QuerySelector(".container-img img")?.GetAttribute("src");

            if (string.IsNullOrEmpty(teamName)) return null;

            // QoP data cells use col-sm-3 (not col-sm-1 like standard standings)
            var dataCells = row.QuerySelectorAll(".col-sm-3.pad-0.hidden-xs").ToList();

            var mp  = dataCells.Count > 0 && int.TryParse(dataCells[0].TextContent?.Trim(), out var mp_) ? mp_ : 0;
            var att = dataCells.Count > 1 && decimal.TryParse(dataCells[1].TextContent?.Trim(),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var att_) ? att_ : 0m;
            var def = dataCells.Count > 2 && decimal.TryParse(dataCells[2].TextContent?.Trim(),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var def_) ? def_ : 0m;
            var qop = dataCells.Count > 3 && decimal.TryParse(dataCells[3].TextContent?.Trim(),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qop_) ? qop_ : 0m;

            // Qualification status
            string? qualification = null;
            if (row.QuerySelector(".mark-qop-championship") != null)
                qualification = "championship";
            else if (row.QuerySelector(".mark-qop-premier") != null)
                qualification = "premier";

            return new QoPRankingDto
            {
                Rank           = rank,
                TeamName       = teamName,
                LogoUrl        = logoUrl,
                DivisionName   = divisionName,
                MatchesPlayed  = mp,
                AttScore       = att,
                DefScore       = def,
                QualityOfPlay  = qop,
                Qualification  = qualification,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse a QoP row");
            return null;
        }
    }

    public class QoPRankingDto
    {
        public int Rank { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string DivisionName { get; set; } = string.Empty;
        public int MatchesPlayed { get; set; }
        public decimal AttScore { get; set; }
        public decimal DefScore { get; set; }
        public decimal QualityOfPlay { get; set; }
        public string? Qualification { get; set; }
    }

    public class StandingsGroupDto
    {
        public string RegionName { get; set; } = string.Empty;
        public List<StandingRowDto> Standings { get; set; } = new();
    }

    public class StandingRowDto
    {
        public int Rank { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public int GP { get; set; }
        public int W { get; set; }
        public int D { get; set; }
        public int L { get; set; }
        public decimal Sors { get; set; }
        public decimal GF { get; set; }
        public decimal GA { get; set; }
        public decimal GD { get; set; }
        public int Pts { get; set; }
        public decimal PPM { get; set; }
        public decimal WPM { get; set; }
        public decimal GDPM { get; set; }
        public decimal GPM { get; set; }
    }

    private async Task ComputeSorsAsync(List<StandingsGroupDto> groups, string program, string ageGroup)
    {
        var isAcademy   = program.Equals("academy",   StringComparison.OrdinalIgnoreCase);
        var isHomegrown = program.Equals("homegrown", StringComparison.OrdinalIgnoreCase);

        var allMatches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.AgeGroup)
            .Include(m => m.Region)
                .ThenInclude(r => r.Division)
            .Include(m => m.Competition)
            .Where(m =>
                (isAcademy
                    ? (m.Region.Division.TournamentId == TournamentConstants.AcademyTournamentId || m.Competition.Name.StartsWith("AD"))
                    : (new[] { TournamentConstants.HomegrownTournamentId, TournamentConstants.FestTournamentId }.Contains(m.Region.Division.TournamentId) && !m.Competition.Name.StartsWith("AD"))) &&
                m.AgeGroup.Name == ageGroup &&
                m.Competition.Name != "MLS NEXT Flex (Regular Season)")
            .ToListAsync();

        var completedMatches = allMatches
            .Where(m => m.Score != null && m.Score != "" && m.Score != "TBD")
            .ToList();
        var remainingMatches = allMatches
            .Where(m => m.Score == null || m.Score == "" || m.Score == "TBD")
            .ToList();

        foreach (var group in groups)
        {
            // Scope both PPG and remaining matches to this region so SORS matches
            // what the user sees in the regional standings table.
            var regionCompleted = completedMatches
                .Where(m => m.Region.Name == group.RegionName)
                .ToList();
            var regionRemaining = remainingMatches
                .Where(m => m.Region.Name == group.RegionName)
                .ToList();

            var teamPpg = BuildTeamPpgByName(regionCompleted);

            foreach (var row in group.Standings)
            {
                var name = row.TeamName.Trim();

                var opponentNames = regionRemaining
                    .Where(m =>
                        string.Equals(m.HomeTeam.Name.Trim(), name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m.AwayTeam.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
                    .Select(m =>
                        string.Equals(m.HomeTeam.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)
                            ? m.AwayTeam.Name.Trim()
                            : m.HomeTeam.Name.Trim())
                    .ToList();

                if (opponentNames.Count == 0)
                {
                    row.Sors = 0m;
                    continue;
                }

                var opponentPpgs = opponentNames
                    .Select(n => teamPpg.TryGetValue(n.ToLowerInvariant(), out var ppg) ? ppg : 0.0)
                    .ToList();

                row.Sors = (decimal)Math.Round(opponentPpgs.Average(), 2);
            }
        }
    }

    private static Dictionary<string, double> BuildTeamPpgByName(List<YSS.Data.Entities.Match> completedMatches)
    {
        var wins   = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var draws  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var played = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in completedMatches)
        {
            if (!TryParseScore(m.Score!, out var homeGoals, out var awayGoals)) continue;

            var home = m.HomeTeam.Name.Trim();
            var away = m.AwayTeam.Name.Trim();

            foreach (var t in new[] { home, away })
            {
                if (!played.ContainsKey(t)) { played[t] = 0; wins[t] = 0; draws[t] = 0; }
            }

            played[home]++; played[away]++;

            if (homeGoals > awayGoals)      wins[home]++;
            else if (homeGoals < awayGoals) wins[away]++;
            else                            { draws[home]++; draws[away]++; }
        }

        return played
            .Where(kvp => kvp.Value > 0)
            .ToDictionary(
                kvp => kvp.Key.ToLowerInvariant(),
                kvp => (double)(wins[kvp.Key] * 3 + draws[kvp.Key]) / kvp.Value);
    }

    private static bool TryParseScore(string score, out int homeScore, out int awayScore)
    {
        homeScore = 0; awayScore = 0;
        if (string.IsNullOrWhiteSpace(score)) return false;
        var parts = score.Split('-');
        if (parts.Length != 2) return false;
        return int.TryParse(parts[0].Trim(), out homeScore) &&
               int.TryParse(parts[1].Trim(), out awayScore);
    }
}
