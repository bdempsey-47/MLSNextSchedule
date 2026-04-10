using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using YSS.Data;
using Microsoft.EntityFrameworkCore;
using System.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

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

            // Pagination parameters
            var pageSize = int.TryParse(queryParams["pageSize"], out var ps) ? Math.Min(ps, 500) : 100;
            var offset = int.TryParse(queryParams["offset"], out var o) ? Math.Max(o, 0) : 0;

            _logger.LogInformation("GetMatches called with: league={League}, seasons={Seasons}, programs={Programs}, startDate={StartDate}, endDate={EndDate}, pageSize={PageSize}, offset={Offset}",
                league ?? "(all)", string.Join(",", seasons), string.Join(",", programs), startDateStr ?? "(none)", endDateStr ?? "(none)", pageSize, offset);

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

            // Get total count before pagination
            var totalCount = await matches.CountAsync();

            var results = await matches
                .OrderBy(m => m.MatchDateUtc)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync();

            _logger.LogInformation("GetMatches returning {Count}/{Total} results (offset={Offset}, pageSize={PageSize})", results.Count, totalCount, offset, pageSize);

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

            // Compute ELO ranks scoped to the requested program(s) + age groups
            // to avoid mixing Academy and Homegrown rankings together
            IQueryable<YSS.Data.Entities.Match> programScopeQuery = _context.Matches
                .Where(m => ageGroupIds.Contains(m.AgeGroupId));

            if (programs.Any())
            {
                var normalizedPrograms2 = programs.Select(p => p.ToLower()).ToList();
                var isHomegrown2 = normalizedPrograms2.Contains("homegrown");
                var isAcademy2 = normalizedPrograms2.Contains("academy");
                programScopeQuery = programScopeQuery.Where(m =>
                    (isAcademy2 && (
                        m.Region.Division.TournamentId == 35 ||
                        m.Region.Division.TournamentId == 84 ||
                        m.Competition.Name.StartsWith("AD"))) ||
                    (isHomegrown2 && (
                        new[] { 12, 75 }.Contains(m.Region.Division.TournamentId) &&
                        !m.Competition.Name.StartsWith("AD"))));
            }

            var programTeamIds = await programScopeQuery
                .Select(m => m.HomeTeamId)
                .Union(programScopeQuery.Select(m => m.AwayTeamId))
                .ToListAsync();

            var allElosForAgeGroups = await _context.TeamAgeGroupElos
                .Where(e => ageGroupIds.Contains(e.AgeGroupId) &&
                            (!programs.Any() || programTeamIds.Contains(e.TeamId)))
                .ToListAsync();

            var rankLookup = new Dictionary<(int teamId, int ageGroupId), (int rank, int total)>();
            foreach (var ageGroupId in ageGroupIds)
            {
                var sorted = allElosForAgeGroups
                    .Where(e => e.AgeGroupId == ageGroupId)
                    .OrderByDescending(e => e.EloRating)
                    .ToList();
                for (int i = 0; i < sorted.Count; i++)
                    rankLookup[(sorted[i].TeamId, ageGroupId)] = (i + 1, sorted.Count);
            }

            // Overlay ELO ranks onto teams
            foreach (var match in results)
            {
                if (rankLookup.TryGetValue((match.HomeTeamId, match.AgeGroupId), out var homeRank))
                {
                    match.HomeTeam.EloRank = homeRank.rank;
                    match.HomeTeam.EloTotal = homeRank.total;
                }
                if (rankLookup.TryGetValue((match.AwayTeamId, match.AgeGroupId), out var awayRank))
                {
                    match.AwayTeam.EloRank = awayRank.rank;
                    match.AwayTeam.EloTotal = awayRank.total;
                }
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            response.Headers.Add("Content-Type", "application/json");

            // Custom JSON serialization to include [NotMapped] properties
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            // Wrap results with pagination metadata
            var paginatedResponse = new
            {
                matches = results,
                totalCount = totalCount,
                pageSize = pageSize,
                offset = offset,
                hasMore = offset + results.Count < totalCount
            };

            var json = JsonSerializer.Serialize(paginatedResponse, jsonOptions);
            await response.WriteStringAsync(json);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatches: {Message}", ex.Message);
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            errorResponse.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            errorResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
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
