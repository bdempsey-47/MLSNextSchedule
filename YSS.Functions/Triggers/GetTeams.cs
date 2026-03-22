using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using YSS.Data;
using Microsoft.EntityFrameworkCore;
using System.Web;

namespace YSS.Functions.Triggers;

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
            var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
            var league   = queryParams["league"]  ?? string.Empty;
            var programs = queryParams.GetValues("program")?.ToList() ?? new List<string>();
            var seasons  = queryParams.GetValues("season")?.ToList() ?? new List<string>();
            var region   = queryParams["region"]  ?? string.Empty;

            var matchQuery = _context.Matches
                .Include(m => m.Region).ThenInclude(r => r.Division).ThenInclude(d => d.League)
                .AsQueryable();

            // Filter by league
            if (!string.IsNullOrEmpty(league))
                matchQuery = matchQuery.Where(m => m.Region.Division.League.Name == league);

            // Filter by programs (match GetMatches.cs pattern: competition-based + tournament-based)
            if (programs.Any())
            {
                var isAcademy = programs.Any(p => p.ToLower() == "academy");
                var isHomegrown = programs.Any(p => p.ToLower() == "homegrown");

                matchQuery = matchQuery
                    .Include(m => m.Competition)
                    .Where(m =>
                        (isAcademy && (m.Region.Division.TournamentId == 35 ||
                            m.Competition.Name.StartsWith("AD"))) ||
                        (isHomegrown && (new[] { 12, 75 }.Contains(m.Region.Division.TournamentId) &&
                            !m.Competition.Name.StartsWith("AD"))));
            }

            // Filter by seasons
            var (seasonStart, seasonEnd) = ParseSeasons(seasons);
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
                .Select(t => new { t.Id, t.Name, t.Program })
                .ToListAsync();

            _logger.LogInformation("GetTeams returning {Count} teams (programs={Programs}, seasons={Seasons}, region={Region})",
                teams.Count, string.Join(",", programs), string.Join(",", seasons), string.IsNullOrEmpty(region) ? "(all)" : region);

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

    private (DateTime? StartDate, DateTime? EndDate) ParseSeasons(List<string> seasons)
    {
        if (seasons == null || !seasons.Any())
            return (null, null);

        DateTime? minStart = null;
        DateTime? maxEnd = null;

        foreach (var season in seasons)
        {
            var (start, end) = season.ToLower() switch
            {
                "fall2025"   => (new DateTime(2025, 7, 1), new DateTime(2025, 12, 31, 23, 59, 59)),
                "spring2026" => (new DateTime(2026, 1, 1), new DateTime(2026, 6, 30, 23, 59, 59)),
                _ => ((DateTime?)null, (DateTime?)null)
            };

            if (start.HasValue && (!minStart.HasValue || start.Value < minStart.Value))
                minStart = start;
            if (end.HasValue && (!maxEnd.HasValue || end.Value > maxEnd.Value))
                maxEnd = end;
        }

        return (minStart, maxEnd);
    }
}
