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

            var oneYearAgo = DateTime.UtcNow.AddYears(-1);

            // Load all data sequentially (DbContext is not thread-safe)
            var eloData = await _context.TeamAgeGroupElos
                .Include(e => e.Team)
                .Include(e => e.AgeGroup)
                .Where(e => e.EloRating != 1500)
                .ToListAsync();

            var allMatches = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.AgeGroup)
                .Include(m => m.Region)
                    .ThenInclude(r => r.Division)
                .Include(m => m.Competition)
                .Where(m => m.MatchDateUtc >= oneYearAgo)
                .ToListAsync();

            var totalTeams = await _context.Teams.CountAsync();

            // Count only real geographic regions — exclude single-letter groups (A-Z),
            // tournament brackets (Playoff, Finals, Consolation, NEXT Pro),
            // MLS Academy, and "Pro Player Pathway" duplicates of existing regions
            var totalRegions = await _context.Regions
                .Where(r => r.Name.Length > 1)                          // exclude A, B, C, ...
                .Where(r => !r.Name.EndsWith("(Pro Player Pathway)"))   // dupes of real regions
                .Where(r => r.Name != "MLS Academy")
                .Where(r => r.Name != "FEST")
                .Where(r => r.Name != "Consolation")
                .Where(r => r.Name != "Playoff")
                .Where(r => r.Name != "Playoffs")
                .Where(r => r.Name != "Finals")
                .Where(r => r.Name != "NEXT Pro")
                .CountAsync();

            // === 1. Top 5 ELO per program per age group ===
            var academyTopElo = BuildTopElo(eloData, allMatches, "AG");
            var homegrownTopElo = BuildTopElo(eloData, allMatches, "HG");

            // === 2. FEST Region Dominance ===
            var (festHgRegions, festAdRegions) = BuildFestDominance(allMatches);

            // === 3. Biggest Upsets per age group ===
            var biggestUpsets = BuildBiggestUpsets(allMatches, eloData);

            // === 4. Match of the Week per age group ===
            var matchesOfTheWeek = BuildMatchesOfTheWeek(allMatches, eloData);

            // === 5. Quick Stats ===
            var completedMatches = allMatches.Count(m => !string.IsNullOrEmpty(m.Score) && m.Score != "TBD");
            var quickStats = new QuickStatsDto
            {
                TotalMatches = allMatches.Count,
                TotalTeams = totalTeams,
                TotalRegions = totalRegions,
                CompletedMatches = completedMatches
            };

            var result = new HomepageStatsDto
            {
                AcademyTopElo = academyTopElo,
                HomegrownTopElo = homegrownTopElo,
                FestHomegrownRegions = festHgRegions,
                FestAcademyRegions = festAdRegions,
                BiggestUpsets = biggestUpsets,
                MatchesOfTheWeek = matchesOfTheWeek,
                QuickStats = quickStats
            };

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            response.Headers.Add("Cache-Control", "public, max-age=3600");
            await response.WriteAsJsonAsync(result);
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

    private Dictionary<string, List<MiniRankingDto>> BuildTopElo(
        List<Data.Entities.TeamAgeGroupElo> eloData,
        List<Data.Entities.Match> allMatches,
        string program)
    {
        // Build primary region lookup: teamId -> most frequent region name (excluding FEST)
        var teamRegions = BuildTeamPrimaryRegions(allMatches);

        // Build ELO deltas from recent matches using EloCalculator
        var deltasByProgramAgeGroup = BuildEloDeltas(allMatches, program);

        var result = new Dictionary<string, List<MiniRankingDto>>();

        var grouped = eloData
            .Where(e => e.Team.Program == program)
            .GroupBy(e => NormalizeAgeGroup(e.AgeGroup.Name));

        foreach (var group in grouped)
        {
            var ageGroupName = group.Key;
            var deltas = deltasByProgramAgeGroup.GetValueOrDefault(ageGroupName);

            var top5 = group
                .OrderByDescending(e => e.EloRating)
                .Take(5)
                .Select((e, i) => new MiniRankingDto
                {
                    Rank = i + 1,
                    TeamName = e.Team.Name,
                    LogoUrl = e.Team.LogoUrl,
                    RegionName = teamRegions.GetValueOrDefault(e.TeamId, ""),
                    EloRating = e.EloRating,
                    EloDelta = deltas != null && deltas.TryGetValue(e.TeamId, out var d)
                        ? Math.Round(d, 1) : 0.0
                })
                .ToList();

            if (top5.Count > 0)
                result[ageGroupName] = top5;
        }

        return result;
    }

    private Dictionary<int, string> BuildTeamPrimaryRegions(List<Data.Entities.Match> allMatches)
    {
        var regionCounts = new Dictionary<int, Dictionary<string, int>>();

        foreach (var m in allMatches)
        {
            if (m.Region.Name == "FEST") continue;

            foreach (var teamId in new[] { m.HomeTeamId, m.AwayTeamId })
            {
                if (!regionCounts.ContainsKey(teamId))
                    regionCounts[teamId] = new Dictionary<string, int>();
                var counts = regionCounts[teamId];
                counts[m.Region.Name] = counts.GetValueOrDefault(m.Region.Name) + 1;
            }
        }

        return regionCounts.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.OrderByDescending(x => x.Value).First().Key);
    }

    private Dictionary<string, Dictionary<int, double>> BuildEloDeltas(
        List<Data.Entities.Match> allMatches, string program)
    {
        // Group completed matches by age group for this program, compute ELO deltas
        var result = new Dictionary<string, Dictionary<int, double>>();
        var isAcademy = program == "AG";

        var programMatches = allMatches
            .Where(m => !string.IsNullOrEmpty(m.Score) && m.Score != "TBD")
            .Where(m => isAcademy
                ? (m.Region.Division.TournamentId == 35 || m.Competition.Name.StartsWith("AD"))
                : (new[] { 12, 75 }.Contains(m.Region.Division.TournamentId) && !m.Competition.Name.StartsWith("AD")))
            .GroupBy(m => NormalizeAgeGroup(m.AgeGroup.Name));

        foreach (var group in programMatches)
        {
            var inputs = group
                .OrderBy(m => m.MatchDateUtc)
                .Select(m =>
                {
                    TryParseScore(m.Score!, out var hs, out var aws);
                    return new EloCalculator.EloMatchInput(m.HomeTeamId, m.AwayTeamId, hs, aws, m.MatchDateUtc);
                })
                .ToList();

            var eloResults = EloCalculator.ComputeRankings(inputs);
            result[group.Key] = eloResults.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.RecentDeltas.Sum());
        }

        return result;
    }

    private (List<RegionDominanceDto> hg, List<RegionDominanceDto> ad) BuildFestDominance(
        List<Data.Entities.Match> allMatches)
    {
        // FEST matches have Region.Name == "FEST"
        var festMatches = allMatches
            .Where(m => m.Region.Name == "FEST" && !string.IsNullOrEmpty(m.Score) && m.Score != "TBD")
            .ToList();

        if (festMatches.Count == 0)
            return (new List<RegionDominanceDto>(), new List<RegionDominanceDto>());

        // Build team -> primary region lookup (from non-FEST matches)
        var teamRegions = BuildTeamPrimaryRegions(allMatches);

        // Aggregate by (program, primary region)
        var regionStats = new Dictionary<(string program, string region), RegionStats>();

        foreach (var m in festMatches)
        {
            if (!TryParseScore(m.Score!, out var hs, out var aws)) continue;

            var homeProgram = m.HomeTeam.Program;
            var awayProgram = m.AwayTeam.Program;
            var homeRegion = teamRegions.GetValueOrDefault(m.HomeTeamId, "Unknown");
            var awayRegion = teamRegions.GetValueOrDefault(m.AwayTeamId, "Unknown");

            // Home team stats
            var homeKey = (homeProgram, homeRegion);
            if (!regionStats.ContainsKey(homeKey))
                regionStats[homeKey] = new RegionStats();
            var home = regionStats[homeKey];
            home.GoalsFor += hs;
            home.GoalsAgainst += aws;
            if (hs > aws) home.Wins++;
            else if (hs < aws) home.Losses++;

            // Away team stats
            var awayKey = (awayProgram, awayRegion);
            if (!regionStats.ContainsKey(awayKey))
                regionStats[awayKey] = new RegionStats();
            var away = regionStats[awayKey];
            away.GoalsFor += aws;
            away.GoalsAgainst += hs;
            if (aws > hs) away.Wins++;
            else if (aws < hs) away.Losses++;
        }

        var hg = regionStats
            .Where(kvp => kvp.Key.program == "HG" && kvp.Key.region != "Unknown")
            .OrderByDescending(kvp => kvp.Value.GoalsFor - kvp.Value.GoalsAgainst)
            .Take(3)
            .Select((kvp, i) => new RegionDominanceDto
            {
                Rank = i + 1,
                RegionName = kvp.Key.region,
                Wins = kvp.Value.Wins,
                Losses = kvp.Value.Losses,
                GoalsFor = kvp.Value.GoalsFor,
                GoalsAgainst = kvp.Value.GoalsAgainst,
                GoalDifference = kvp.Value.GoalsFor - kvp.Value.GoalsAgainst
            })
            .ToList();

        var ad = regionStats
            .Where(kvp => kvp.Key.program == "AG" && kvp.Key.region != "Unknown")
            .OrderByDescending(kvp => kvp.Value.GoalsFor - kvp.Value.GoalsAgainst)
            .Take(3)
            .Select((kvp, i) => new RegionDominanceDto
            {
                Rank = i + 1,
                RegionName = kvp.Key.region,
                Wins = kvp.Value.Wins,
                Losses = kvp.Value.Losses,
                GoalsFor = kvp.Value.GoalsFor,
                GoalsAgainst = kvp.Value.GoalsAgainst,
                GoalDifference = kvp.Value.GoalsFor - kvp.Value.GoalsAgainst
            })
            .ToList();

        return (hg, ad);
    }

    private Dictionary<string, UpsetDto> BuildBiggestUpsets(
        List<Data.Entities.Match> allMatches,
        List<Data.Entities.TeamAgeGroupElo> eloData)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        // Build ELO lookup: (teamId, ageGroupId) -> rating
        var eloLookup = eloData.ToDictionary(
            e => (e.TeamId, e.AgeGroupId),
            e => e.EloRating);

        var completedRecent = allMatches
            .Where(m => !string.IsNullOrEmpty(m.Score) && m.Score != "TBD"
                        && m.MatchDateUtc >= thirtyDaysAgo && m.MatchDateUtc <= now)
            .ToList();

        var result = new Dictionary<string, UpsetDto>();

        foreach (var group in completedRecent.GroupBy(m => NormalizeAgeGroup(m.AgeGroup.Name)))
        {
            UpsetDto? biggestUpset = null;
            var maxEloDiff = 0;

            foreach (var m in group)
            {
                if (!TryParseScore(m.Score!, out var hs, out var aws)) continue;
                if (hs == aws) continue; // draws aren't upsets

                var homeElo = eloLookup.GetValueOrDefault((m.HomeTeamId, m.AgeGroupId), 1500);
                var awayElo = eloLookup.GetValueOrDefault((m.AwayTeamId, m.AgeGroupId), 1500);

                var homeWon = hs > aws;
                var winnerElo = homeWon ? homeElo : awayElo;
                var loserElo = homeWon ? awayElo : homeElo;

                // Only upset if lower-ELO team won (underdog victory)
                if (winnerElo >= loserElo) continue;

                var eloDiff = loserElo - winnerElo; // positive = upset magnitude

                if (eloDiff > maxEloDiff)
                {
                    maxEloDiff = eloDiff;
                    var program = m.HomeTeam.Program == "AG" ? "MLS Next Academy" : "MLS Next Homegrown";

                    biggestUpset = new UpsetDto
                    {
                        HomeTeamName = m.HomeTeam.Name,
                        HomeLogoUrl = m.HomeTeam.LogoUrl,
                        HomeElo = homeElo,
                        AwayTeamName = m.AwayTeam.Name,
                        AwayLogoUrl = m.AwayTeam.LogoUrl,
                        AwayElo = awayElo,
                        Score = m.Score!,
                        HomeWon = homeWon,
                        EloDiff = eloDiff,
                        MatchDate = m.MatchDateUtc.ToString("yyyy-MM-dd"),
                        Program = program
                    };
                }
            }

            if (biggestUpset != null)
                result[group.Key] = biggestUpset;
        }

        return result;
    }

    private Dictionary<string, MatchOfWeekDto> BuildMatchesOfTheWeek(
        List<Data.Entities.Match> allMatches,
        List<Data.Entities.TeamAgeGroupElo> eloData)
    {
        var now = DateTime.UtcNow;
        var sevenDaysFromNow = now.AddDays(7);

        var eloLookup = eloData.ToDictionary(
            e => (e.TeamId, e.AgeGroupId),
            e => e.EloRating);

        // Upcoming matches (no score yet, within next 7 days)
        var upcoming = allMatches
            .Where(m => (string.IsNullOrEmpty(m.Score) || m.Score == "TBD")
                        && m.MatchDateUtc >= now && m.MatchDateUtc <= sevenDaysFromNow)
            .ToList();

        var result = new Dictionary<string, MatchOfWeekDto>();

        foreach (var group in upcoming.GroupBy(m => NormalizeAgeGroup(m.AgeGroup.Name)))
        {
            var best = group
                .Select(m =>
                {
                    var homeElo = eloLookup.GetValueOrDefault((m.HomeTeamId, m.AgeGroupId), 1500);
                    var awayElo = eloLookup.GetValueOrDefault((m.AwayTeamId, m.AgeGroupId), 1500);
                    return (match: m, homeElo, awayElo, combined: homeElo + awayElo);
                })
                .OrderByDescending(x => x.combined)
                .FirstOrDefault();

            if (best.match != null && best.combined > 3000) // both teams have some ELO history
            {
                var program = best.match.HomeTeam.Program == "AG" ? "MLS Next Academy" : "MLS Next Homegrown";
                result[group.Key] = new MatchOfWeekDto
                {
                    HomeTeamName = best.match.HomeTeam.Name,
                    HomeLogoUrl = best.match.HomeTeam.LogoUrl,
                    HomeElo = best.homeElo,
                    AwayTeamName = best.match.AwayTeam.Name,
                    AwayLogoUrl = best.match.AwayTeam.LogoUrl,
                    AwayElo = best.awayElo,
                    MatchDate = best.match.MatchDateUtc.ToString("yyyy-MM-dd"),
                    CombinedElo = best.combined,
                    Program = program
                };
            }
        }

        // Fallback: if no upcoming matches for an age group, use most recent high-ELO completed match
        var recentCompleted = allMatches
            .Where(m => !string.IsNullOrEmpty(m.Score) && m.Score != "TBD")
            .OrderByDescending(m => m.MatchDateUtc)
            .ToList();

        foreach (var group in recentCompleted.GroupBy(m => NormalizeAgeGroup(m.AgeGroup.Name)))
        {
            if (result.ContainsKey(group.Key)) continue;

            var best = group
                .Take(50) // only look at recent matches
                .Select(m =>
                {
                    var homeElo = eloLookup.GetValueOrDefault((m.HomeTeamId, m.AgeGroupId), 1500);
                    var awayElo = eloLookup.GetValueOrDefault((m.AwayTeamId, m.AgeGroupId), 1500);
                    return (match: m, homeElo, awayElo, combined: homeElo + awayElo);
                })
                .OrderByDescending(x => x.combined)
                .FirstOrDefault();

            if (best.match != null && best.combined > 3000)
            {
                var program = best.match.HomeTeam.Program == "AG" ? "MLS Next Academy" : "MLS Next Homegrown";
                result[group.Key] = new MatchOfWeekDto
                {
                    HomeTeamName = best.match.HomeTeam.Name,
                    HomeLogoUrl = best.match.HomeTeam.LogoUrl,
                    HomeElo = best.homeElo,
                    AwayTeamName = best.match.AwayTeam.Name,
                    AwayLogoUrl = best.match.AwayTeam.LogoUrl,
                    AwayElo = best.awayElo,
                    MatchDate = best.match.MatchDateUtc.ToString("yyyy-MM-dd"),
                    CombinedElo = best.combined,
                    Program = program
                };
            }
        }

        return result;
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

    private static string NormalizeAgeGroup(string name) => name switch
    {
        "U19"     => "U18/19",
        "U18/U19" => "U18/19",
        "U18-19"  => "U18/19",
        _         => name
    };

    // === Helper class ===
    private class RegionStats
    {
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
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
