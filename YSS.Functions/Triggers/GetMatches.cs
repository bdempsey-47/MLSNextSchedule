using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using YSS.Data;
using Microsoft.EntityFrameworkCore;
using System.Web;

namespace YSS.Functions.Triggers;

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
            // Parse query parameters - use HttpUtility to handle multiple values
            var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
            
            var league = queryParams["league"] ?? string.Empty;
            var query = queryParams["team"] ?? string.Empty;
            var seasons = queryParams.GetValues("season")?.ToList() ?? new List<string>();
            var startDateStr = queryParams["startDate"] ?? string.Empty;
            var endDateStr = queryParams["endDate"] ?? string.Empty;
            var ageGroups = queryParams.GetValues("ageGroup")?.ToList() ?? new List<string>();
            var division = queryParams["division"] ?? string.Empty;
            
            // Support multiple program values: ?program=homegrown&program=academy
            var programs = queryParams.GetValues("program")?.ToList() ?? new List<string>();

            _logger.LogInformation("GetMatches called with: league={League}, seasons={Seasons}, programs={Programs}, startDate={StartDate}, endDate={EndDate}", 
                league ?? "(all)", string.Join(",", seasons), string.Join(",", programs), startDateStr ?? "(none)", endDateStr ?? "(none)");

            var matches = _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Venue)
                .Include(m => m.AgeGroup)
                .Include(m => m.Region)
                .ThenInclude(r => r.Division)
                .ThenInclude(d => d.League)
                .Include(m => m.Competition)
                .AsQueryable();

            // Filter by league
            if (!string.IsNullOrEmpty(league))
            {
                _logger.LogInformation("Filtering by league: {League}", league);
                matches = matches.Where(m => m.Region.Division.League.Name == league);
            }

            // Filter by programs using competition name to split showcase matches:
            // Academy = tournament 35 OR "AD Showcase"/"AD" competition (any tournament)
            // Homegrown = tournament 12/75 matches that aren't "AD Showcase"/"AD"
            if (programs.Any())
            {
                var normalizedPrograms = programs.Select(p => p.ToLower()).ToList();
                var isHomegrown = normalizedPrograms.Contains("homegrown");
                var isAcademy = normalizedPrograms.Contains("academy");

                _logger.LogInformation("Filtering by programs: {Programs}", string.Join(", ", programs));

                matches = matches.Where(m =>
                    (isAcademy && (
                        m.Region.Division.TournamentId == 35 ||
                        m.Region.Division.TournamentId == 84 ||
                        m.Competition.Name.StartsWith("AD"))) ||
                    (isHomegrown && (
                        new[] { 12, 75 }.Contains(m.Region.Division.TournamentId) &&
                        !m.Competition.Name.StartsWith("AD"))));
            }

            // Map seasons to combined date range
            var (seasonStartDate, seasonEndDate) = ParseSeasons(seasons);

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
            if (ageGroups.Any())
            {
                matches = matches.Where(m => ageGroups.Contains(m.AgeGroup.Name));
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

            // Load per-age-group ELO ratings for all teams in the result set
            var teamIds = results.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }).Distinct().ToList();
            var ageGroupIds = results.Select(m => m.AgeGroupId).Distinct().ToList();
            var eloLookup = await _context.TeamAgeGroupElos
                .Where(e => teamIds.Contains(e.TeamId) && ageGroupIds.Contains(e.AgeGroupId))
                .ToDictionaryAsync(e => (e.TeamId, e.AgeGroupId), e => e.EloRating);

            // Overlay per-age-group ELO onto teams before serialization
            foreach (var match in results)
            {
                if (eloLookup.TryGetValue((match.HomeTeamId, match.AgeGroupId), out var homeElo))
                    match.HomeTeam.EloRating = homeElo;
                if (eloLookup.TryGetValue((match.AwayTeamId, match.AgeGroupId), out var awayElo))
                    match.AwayTeam.EloRating = awayElo;
            }

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
    /// Parse multiple season parameters to combined date range.
    /// If both fall2025 and spring2026 are selected, returns the union (Jul 2025 - Jun 2026).
    /// </summary>
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
                "fall2025" => (new DateTime(2025, 7, 1), new DateTime(2025, 12, 31, 23, 59, 59)),
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
