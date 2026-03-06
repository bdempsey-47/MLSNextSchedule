using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Web;

namespace YSS.Functions.Triggers;

public class GetStandings
{
    private readonly IHttpClientFactory _httpClientFactory;
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

    public GetStandings(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
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

            var uidEvent = program.ToLower() switch
            {
                "homegrown" => 12,
                "academy"   => 35,
                _           => (int?)null
            };
            if (!uidEvent.HasValue)
                return await BadRequest(req, "Invalid program. Use 'homegrown' or 'academy'");

            if (!AgeGroupMap.TryGetValue(ageGroup, out var uidAge))
                return await BadRequest(req, $"Unknown age group: {ageGroup}");

            var url = $"https://www.modular11.com/public_schedule/league/get_teams" +
                      $"?tournament_type=league&UID_event={uidEvent}&UID_age={uidAge}&UID_gender=1&list_type=53";

            _logger.LogInformation("Fetching Modular11 standings: {Url}", url);

            var client = _httpClientFactory.CreateClient("standings");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("accept", "text/html, */*; q=0.01");
            request.Headers.Add("x-requested-with", "XMLHttpRequest");
            request.Headers.Add("referer", "https://www.modular11.com/standings?year=21&gender=1");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");

            var httpResponse = await client.SendAsync(request);
            httpResponse.EnsureSuccessStatusCode();
            var html = await httpResponse.Content.ReadAsStringAsync();

            _logger.LogInformation("Modular11 response size: {Length} chars", html.Length);

            var groups = ParseStandings(html);
            _logger.LogInformation("Parsed {Count} standing groups", groups.Count);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await response.WriteAsJsonAsync(groups);
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

    private List<StandingsGroupDto> ParseStandings(string html)
    {
        var context  = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

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

            // Desktop hidden cells: MP (0), W (1), L (2), T/draw (3)
            var desktopCells = row.QuerySelectorAll(".col-sm-1.pad-0.gap-right-mobile-sm.hidden-xs").ToList();
            var gp = desktopCells.Count > 0 && int.TryParse(desktopCells[0].TextContent?.Trim(), out var gp_) ? gp_ : 0;
            var w  = desktopCells.Count > 1 && int.TryParse(desktopCells[1].TextContent?.Trim(), out var w_)  ? w_  : 0;
            var l  = desktopCells.Count > 2 && int.TryParse(desktopCells[2].TextContent?.Trim(), out var l_)  ? l_  : 0;
            var d  = desktopCells.Count > 3 && int.TryParse(desktopCells[3].TextContent?.Trim(), out var d_)  ? d_  : 0;

            return new StandingRowDto
            {
                Rank     = rank,
                TeamName = teamName,
                LogoUrl  = logoUrl,
                GP       = gp,
                W        = w,
                D        = d,
                L        = l,
                Pts      = pts,
                PPM      = ppm
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse a team row");
            return null;
        }
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
        public int Pts { get; set; }
        public decimal PPM { get; set; }
    }
}
