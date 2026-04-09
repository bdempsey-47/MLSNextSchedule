using Microsoft.Extensions.Logging;
using System.Text.Json;
using YSS.Data;
using YSS.Data.Entities;
using YSS.Functions.Triggers;
using Microsoft.EntityFrameworkCore;

namespace YSS.Functions.Services;

/// <summary>
/// Computes and stores pre-computed homepage statistics snapshot.
/// Called nightly to avoid expensive in-memory processing on each request.
/// </summary>
public class HomepageSnapshotService
{
    private readonly AppDbContext _context;
    private readonly ILogger<HomepageSnapshotService> _logger;

    public HomepageSnapshotService(AppDbContext context, ILogger<HomepageSnapshotService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ComputeAndStoreAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Starting homepage snapshot computation");

            var oneYearAgo = DateTime.UtcNow.AddYears(-1);

            // Load all data sequentially (DbContext is not thread-safe)
            var eloData = await _context.TeamAgeGroupElos
                .Include(e => e.Team)
                .Include(e => e.AgeGroup)
                .Where(e => e.EloRating != 1500)
                .ToListAsync(ct);

            var allMatches = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.AgeGroup)
                .Include(m => m.Region)
                    .ThenInclude(r => r.Division)
                .Include(m => m.Competition)
                .Where(m => m.MatchDateUtc >= oneYearAgo)
                .ToListAsync(ct);

            var totalTeams = await _context.Teams.CountAsync(ct);

            var totalRegions = await _context.Regions
                .Where(r => r.Name.Length > 1)
                .Where(r => !r.Name.EndsWith("(Pro Player Pathway)"))
                .Where(r => r.Name != "MLS Academy")
                .Where(r => r.Name != "FEST")
                .Where(r => r.Name != "Consolation")
                .Where(r => r.Name != "Playoff")
                .Where(r => r.Name != "Playoffs")
                .Where(r => r.Name != "Finals")
                .Where(r => r.Name != "NEXT Pro")
                .CountAsync(ct);

            // Compute all homepage stats
            var academyTopElo = BuildTopElo(eloData, allMatches, "AG");
            var homegrownTopElo = BuildTopElo(eloData, allMatches, "HG");
            var (festHgRegions, festAdRegions) = BuildFestDominance(allMatches);
            var biggestUpsets = BuildBiggestUpsets(allMatches, eloData);
            var matchesOfTheWeek = BuildMatchesOfTheWeek(allMatches, eloData);

            var completedMatches = allMatches.Count(m => !string.IsNullOrEmpty(m.Score) && m.Score != "TBD");
            var quickStats = new GetHomepageStats.QuickStatsDto
            {
                TotalMatches = allMatches.Count,
                TotalTeams = totalTeams,
                TotalRegions = totalRegions,
                CompletedMatches = completedMatches
            };

            var statsDto = new GetHomepageStats.HomepageStatsDto
            {
                AcademyTopElo = academyTopElo,
                HomegrownTopElo = homegrownTopElo,
                FestHomegrownRegions = festHgRegions,
                FestAcademyRegions = festAdRegions,
                BiggestUpsets = biggestUpsets,
                MatchesOfTheWeek = matchesOfTheWeek,
                QuickStats = quickStats
            };

            // Serialize to JSON
            var statsJson = JsonSerializer.Serialize(statsDto, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Store or update snapshot
            var snapshot = await _context.HomepageSnapshots.FirstOrDefaultAsync(ct);
            if (snapshot == null)
            {
                snapshot = new HomepageSnapshot
                {
                    StatsJson = statsJson,
                    ComputedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _context.HomepageSnapshots.Add(snapshot);
            }
            else
            {
                snapshot.StatsJson = statsJson;
                snapshot.ComputedAtUtc = DateTime.UtcNow;
                snapshot.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Homepage snapshot computed and stored successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing homepage snapshot");
            throw;
        }
    }

    private Dictionary<string, List<GetHomepageStats.MiniRankingDto>> BuildTopElo(
        List<TeamAgeGroupElo> eloData,
        List<Match> allMatches,
        string program)
    {
        var teamRegions = BuildTeamPrimaryRegions(allMatches);
        var deltasByProgramAgeGroup = BuildEloDeltas(allMatches, program);

        var result = new Dictionary<string, List<GetHomepageStats.MiniRankingDto>>();

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
                .Select((e, i) => new GetHomepageStats.MiniRankingDto
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

    private Dictionary<int, string> BuildTeamPrimaryRegions(List<Match> allMatches)
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
        List<Match> allMatches, string program)
    {
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

    private (List<GetHomepageStats.RegionDominanceDto> hg, List<GetHomepageStats.RegionDominanceDto> ad) BuildFestDominance(
        List<Match> allMatches)
    {
        var festMatches = allMatches
            .Where(m => m.Region.Name == "FEST" && !string.IsNullOrEmpty(m.Score) && m.Score != "TBD")
            .ToList();

        if (festMatches.Count == 0)
            return (new List<GetHomepageStats.RegionDominanceDto>(), new List<GetHomepageStats.RegionDominanceDto>());

        var teamRegions = BuildTeamPrimaryRegions(allMatches);
        var regionStats = new Dictionary<(string program, string region), RegionStats>();

        foreach (var m in festMatches)
        {
            if (!TryParseScore(m.Score!, out var hs, out var aws)) continue;

            var homeProgram = m.HomeTeam.Program;
            var awayProgram = m.AwayTeam.Program;
            var homeRegion = teamRegions.GetValueOrDefault(m.HomeTeamId, "Unknown");
            var awayRegion = teamRegions.GetValueOrDefault(m.AwayTeamId, "Unknown");

            var homeKey = (homeProgram, homeRegion);
            if (!regionStats.ContainsKey(homeKey))
                regionStats[homeKey] = new RegionStats();
            var home = regionStats[homeKey];
            home.GoalsFor += hs;
            home.GoalsAgainst += aws;
            if (hs > aws) home.Wins++;
            else if (hs < aws) home.Losses++;

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
            .Select((kvp, i) => new GetHomepageStats.RegionDominanceDto
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
            .Select((kvp, i) => new GetHomepageStats.RegionDominanceDto
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

    private Dictionary<string, GetHomepageStats.UpsetDto> BuildBiggestUpsets(
        List<Match> allMatches,
        List<TeamAgeGroupElo> eloData)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        var eloLookup = eloData.ToDictionary(
            e => (e.TeamId, e.AgeGroupId),
            e => e.EloRating);

        var completedRecent = allMatches
            .Where(m => !string.IsNullOrEmpty(m.Score) && m.Score != "TBD"
                        && m.MatchDateUtc >= thirtyDaysAgo && m.MatchDateUtc <= now)
            .ToList();

        var result = new Dictionary<string, GetHomepageStats.UpsetDto>();

        foreach (var group in completedRecent.GroupBy(m => NormalizeAgeGroup(m.AgeGroup.Name)))
        {
            GetHomepageStats.UpsetDto? biggestUpset = null;
            var maxEloDiff = 0;

            foreach (var m in group)
            {
                if (!TryParseScore(m.Score!, out var hs, out var aws)) continue;
                if (hs == aws) continue;

                var homeElo = eloLookup.GetValueOrDefault((m.HomeTeamId, m.AgeGroupId), 1500);
                var awayElo = eloLookup.GetValueOrDefault((m.AwayTeamId, m.AgeGroupId), 1500);

                var homeWon = hs > aws;
                var winnerElo = homeWon ? homeElo : awayElo;
                var loserElo = homeWon ? awayElo : homeElo;

                if (winnerElo >= loserElo) continue;

                var eloDiff = loserElo - winnerElo;

                if (eloDiff > maxEloDiff)
                {
                    maxEloDiff = eloDiff;
                    var program = m.HomeTeam.Program == "AG" ? "MLS Next Academy" : "MLS Next Homegrown";

                    biggestUpset = new GetHomepageStats.UpsetDto
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

    private Dictionary<string, GetHomepageStats.MatchOfWeekDto> BuildMatchesOfTheWeek(
        List<Match> allMatches,
        List<TeamAgeGroupElo> eloData)
    {
        var now = DateTime.UtcNow;
        var sevenDaysFromNow = now.AddDays(7);

        var eloLookup = eloData.ToDictionary(
            e => (e.TeamId, e.AgeGroupId),
            e => e.EloRating);

        var upcoming = allMatches
            .Where(m => (string.IsNullOrEmpty(m.Score) || m.Score == "TBD")
                        && m.MatchDateUtc >= now && m.MatchDateUtc <= sevenDaysFromNow)
            .ToList();

        var result = new Dictionary<string, GetHomepageStats.MatchOfWeekDto>();

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

            if (best.match != null && best.combined > 3000)
            {
                var program = best.match.HomeTeam.Program == "AG" ? "MLS Next Academy" : "MLS Next Homegrown";
                result[group.Key] = new GetHomepageStats.MatchOfWeekDto
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

        var recentCompleted = allMatches
            .Where(m => !string.IsNullOrEmpty(m.Score) && m.Score != "TBD")
            .OrderByDescending(m => m.MatchDateUtc)
            .ToList();

        foreach (var group in recentCompleted.GroupBy(m => NormalizeAgeGroup(m.AgeGroup.Name)))
        {
            if (result.ContainsKey(group.Key)) continue;

            var best = group
                .Take(50)
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
                result[group.Key] = new GetHomepageStats.MatchOfWeekDto
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
        "U19" => "U18/19",
        "U18/U19" => "U18/19",
        "U18-19" => "U18/19",
        _ => name
    };

    private class RegionStats
    {
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
    }
}
