using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YSS.Data;
using YSS.Ingestion.Models;
using YSS.Ingestion.Services;

// Load configuration
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("local.settings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

// Check for --fest mode
if (args.Length > 0 && args[0] == "--fest")
{
    await RunFestIngestion(args, config);
    return;
}

// Check for --njcup mode
if (args.Length > 0 && args[0] == "--njcup")
{
    await RunNjCupIngestion(args, config);
    return;
}

// If an access token is provided as a CLI argument, set it as an environment variable
if (args.Length > 0)
{
    Environment.SetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN", args[0]);
    Console.WriteLine("Using Azure SQL with access token authentication");
}

var connectionString = config.GetConnectionString("DefaultConnection");
var accessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
var dbInfo = !string.IsNullOrEmpty(accessToken) ? "Azure SQL (token auth)" : connectionString;
Console.WriteLine($"=== MLSNext Full Ingestion Runner ===\nDB: {dbInfo}\n");

// Helper function to configure DbContext with token or connection string
Action<DbContextOptionsBuilder> ConfigureDbContext = options =>
{
    if (!string.IsNullOrEmpty(accessToken))
    {
        var connection = new SqlConnection(
            "Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
        connection.AccessToken = accessToken;
        options.UseSqlServer(connection);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
};

// Run ingestion for each tournament
var tournaments = new[]
{
    new { TournamentId = "35", Label = "Academy F25",   StartDate = "2025-07-01 00:00:01", EndDate = "2025-12-31 23:59:59" },
    new { TournamentId = "12", Label = "Homegrown F25", StartDate = "2025-07-01 00:00:01", EndDate = "2025-12-31 23:59:59" },
    new { TournamentId = "35", Label = "Academy S26",   StartDate = "2026-01-01 00:00:01", EndDate = "2026-06-30 23:59:59" },
    new { TournamentId = "12", Label = "Homegrown S26", StartDate = "2026-01-01 00:00:01", EndDate = "2026-06-30 23:59:59" },
};

foreach (var t in tournaments)
{
    Console.WriteLine(new string('=', 60));
    Console.WriteLine($"Running: {t.Label} (tournament={t.TournamentId}, {t.StartDate} → {t.EndDate})");
    Console.WriteLine(new string('=', 60));

    var settings = new Modular11Settings
    {
        TournamentId = t.TournamentId,
        Gender = "1",
        Status = "scheduled",
        MatchType = "2",
        AgeGroups = new List<string> { "21", "22", "33", "14", "15", "26" },
        StartDate = t.StartDate,
        EndDate = t.EndDate
    };

    var services = new ServiceCollection();
    services.AddHttpClient<Modular11Client>();
    services.AddScoped<ScheduleParser>();
    services.AddScoped<MatchUpsertService>();
    services.AddScoped<IngestionOrchestrator>();
    services.AddDbContext<AppDbContext>(ConfigureDbContext);
    services.AddSingleton(settings);
    services.AddLogging(b => { b.AddConsole(); b.SetMinimumLevel(LogLevel.Information); });

    var sp = services.BuildServiceProvider();
    var orchestrator = sp.GetRequiredService<IngestionOrchestrator>();

    try
    {
        await orchestrator.RunAsync(CancellationToken.None, maxMatches: null, "MLS Next");
        Console.WriteLine($"✅ {t.Label} ingestion complete.\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ {t.Label} failed: {ex.Message}\n");
    }

    await sp.DisposeAsync();
    // Brief pause between tournament runs to avoid hammering the API
    await Task.Delay(2000);
}

Console.WriteLine("\n=== All ingestion runs complete ===");

// Show final counts
{
    var services = new ServiceCollection();
    services.AddDbContext<AppDbContext>(ConfigureDbContext);
    var sp = services.BuildServiceProvider();
    var db = sp.GetRequiredService<AppDbContext>();
    var matchCount = await db.Matches.CountAsync();
    var teamCount  = await db.Teams.CountAsync();
    var teamsWithLogos = await db.Teams.Where(t => t.LogoUrl != null).CountAsync();
    Console.WriteLine($"\nMatches: {matchCount}  |  Teams: {teamCount}  |  With logos: {teamsWithLogos}");
    await sp.DisposeAsync();
}

// --- NJ Cup ingestion helper ---
static async Task RunNjCupIngestion(string[] args, Microsoft.Extensions.Configuration.IConfiguration config)
{
    using var httpClient = new HttpClient();
    Console.WriteLine("=== NJ Cup Ingestion Mode ===");

    // Collect session token from args or prompt
    string sessionToken = args.Length > 1 ? args[1] : string.Empty;
    string? azureToken = args.Length > 2 ? args[2] : null;

    if (string.IsNullOrEmpty(sessionToken))
    {
        Console.Write("Paste the Modular11 session token (_token value): ");
        sessionToken = Console.ReadLine()?.Trim() ?? string.Empty;
    }
    if (string.IsNullOrEmpty(azureToken))
    {
        azureToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
    }

    if (string.IsNullOrEmpty(sessionToken))
    {
        Console.WriteLine("ERROR: Session token is required.");
        return;
    }

    const int tournamentId = 84;
    const string divisionName = "NJ Cup Qualifier";
    var connectionString = config.GetConnectionString("DefaultConnection");

    // Age group mappings (from GetStandings.cs pattern)
    var ageGroups = new Dictionary<string, string>
    {
        ["21"] = "U13",
        ["22"] = "U14",
        ["33"] = "U15",
        ["14"] = "U16",
        ["15"] = "U17",
        ["26"] = "U18/19"
    };

    Action<DbContextOptionsBuilder> configureDb = options =>
    {
        if (!string.IsNullOrEmpty(azureToken))
        {
            var connection = new SqlConnection(
                "Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
            connection.AccessToken = azureToken;
            options.UseSqlServer(connection);
        }
        else
        {
            options.UseSqlServer(connectionString);
        }
    };

    var allParsedMatches = new List<ParsedMatch>();
    var seenMatchIds = new HashSet<string>();
    var parser = new ScheduleParser(LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ScheduleParser>());

    // Loop through each age group
    foreach (var (ageUid, ageGroupName) in ageGroups)
    {
        Console.WriteLine($"\n{'='} Processing {ageGroupName} (UID={ageUid}) {'='}\n");

        // Step 1: Fetch and parse get_teams to discover team IDs for this age group
        string teamsUrl = $"https://www.modular11.com/events/event/get_teams?tournament_type=event&UID_age={ageUid}&UID_gender=1&UID_event=84&list_type=groupplay&open_page=1";

        var teamRequest = new HttpRequestMessage(HttpMethod.Get, teamsUrl);
        teamRequest.Headers.Add("_token", sessionToken);
        teamRequest.Headers.Add("x-csrf-token", sessionToken);
        teamRequest.Headers.Add("x-request-type", "ajax");
        teamRequest.Headers.Add("x-requested-with", "XMLHttpRequest");
        teamRequest.Headers.Add("accept", "text/html, */*; q=0.01");

        HttpResponseMessage teamsResponse;
        try
        {
            teamsResponse = await httpClient.SendAsync(teamRequest);
            teamsResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Skipping {ageGroupName}: Error fetching get_teams - {ex.Message}");
            continue;
        }

        var teamsHtml = await teamsResponse.Content.ReadAsStringAsync();
        var teamIds = ExtractTeamIdsFromGroupPlayHtml(teamsHtml);

        if (teamIds.Count == 0)
        {
            Console.WriteLine($"⚠ {ageGroupName}: No teams found in get_teams response.");
            continue;
        }

        Console.WriteLine($"✓ {ageGroupName}: Discovered {teamIds.Count} teams");

        // Step 2: Fetch matches for each team in this age group
        foreach (var teamId in teamIds)
        {
            string matchUrl = $"https://www.modular11.com/events/event/get_partial_matches_by_team?open_page=1&pagination_data=%5B{teamId}%5D&bracket=39&age={ageUid}&tournament=84&group=1&list_type=groupplay";

            var matchRequest = new HttpRequestMessage(HttpMethod.Get, matchUrl);
            matchRequest.Headers.Add("_token", sessionToken);
            matchRequest.Headers.Add("x-csrf-token", sessionToken);
            matchRequest.Headers.Add("x-request-type", "ajax");
            matchRequest.Headers.Add("x-requested-with", "XMLHttpRequest");
            matchRequest.Headers.Add("accept", "text/html, */*; q=0.01");

            try
            {
                // Throttle requests
                await Task.Delay(Random.Shared.Next(1000, 3000));

                var matchResponse = await httpClient.SendAsync(matchRequest);
                matchResponse.EnsureSuccessStatusCode();
                var matchHtml = await matchResponse.Content.ReadAsStringAsync();

                var parsedMatches = parser.ParseMatches(matchHtml, tournamentId);

                // Override division to NJ Cup Qualifier and set age group
                foreach (var match in parsedMatches)
                {
                    match.Division = divisionName;
                    match.AgeGroup = ageGroupName;  // Override with actual age group
                }

                // Deduplicate by MatchId
                foreach (var match in parsedMatches)
                {
                    if (!seenMatchIds.Contains(match.MatchId))
                    {
                        seenMatchIds.Add(match.MatchId);
                        allParsedMatches.Add(match);
                    }
                }

                Console.WriteLine($"  Team {teamId}: {parsedMatches.Count} matches ({seenMatchIds.Count} unique total)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Team {teamId}: ERROR - {ex.Message}");
            }
        }
    }

    Console.WriteLine($"\n{'='} Summary {'='}\n✓ Collected {allParsedMatches.Count} total matches across all age groups\n");

    if (allParsedMatches.Count == 0)
    {
        Console.WriteLine("❌ No matches found. Aborting.");
        return;
    }

    // Step 3: Upsert to database
    Console.WriteLine("Upserting to database...");

    var services = new ServiceCollection();
    services.AddDbContext<AppDbContext>(configureDb);
    services.AddScoped<MatchUpsertService>();
    services.AddLogging(b => { b.AddConsole(); b.SetMinimumLevel(LogLevel.Information); });

    var sp = services.BuildServiceProvider();
    var upsertService = sp.GetRequiredService<MatchUpsertService>();

    try
    {
        await upsertService.UpsertMatchesAsync(allParsedMatches, "MLS Next", CancellationToken.None);
        Console.WriteLine($"✅ NJ Cup ingestion complete: {allParsedMatches.Count} matches stored.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ NJ Cup ingestion failed: {ex.Message}");
    }
    finally
    {
        await sp.DisposeAsync();
    }
}

static List<int> ExtractTeamIdsFromGroupPlayHtml(string html)
{
    var teamIds = new List<int>();
    try
    {
        var context = AngleSharp.BrowsingContext.New(AngleSharp.Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        // Strategy 1: Try .form_row.main_row[js-group] (league standings pattern from GetStandings)
        var teamRows = document.QuerySelectorAll(".form_row.main_row");

        if (teamRows.Length > 0)
        {
            Console.WriteLine($"  Found {teamRows.Length} team rows via .form_row.main_row");

            foreach (var row in teamRows)
            {
                // Try data-id first
                var dataId = row.GetAttribute("data-id");
                if (!string.IsNullOrEmpty(dataId) && int.TryParse(dataId, out var id))
                {
                    teamIds.Add(id);
                    continue;
                }

                // Try data-team-id
                var dataTeamId = row.GetAttribute("data-team-id");
                if (!string.IsNullOrEmpty(dataTeamId) && int.TryParse(dataTeamId, out var id2))
                {
                    teamIds.Add(id2);
                    continue;
                }

                // Try to extract from href (e.g., /team/12345 or /events/event/team/12345)
                var allLinks = row.QuerySelectorAll("a");
                foreach (var link in allLinks)
                {
                    var href = link.GetAttribute("href") ?? "";
                    var idMatch = System.Text.RegularExpressions.Regex.Match(href, @"[/\?&](?:team|team_id)[=/](\d+)");
                    if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out var id3))
                    {
                        teamIds.Add(id3);
                        break;  // Found team ID in this row, move to next row
                    }
                }
            }
        }

        // Strategy 2: If no rows found, try to find team IDs anywhere in the page
        // Look for patterns like pagination_data=[12345] or team id patterns in links
        if (teamIds.Count == 0)
        {
            Console.WriteLine("  No .form_row.main_row found, searching for team patterns in page...");

            var allLinks = document.QuerySelectorAll("a[href*='get_partial_matches_by_team']");
            foreach (var link in allLinks)
            {
                var href = link.GetAttribute("href") ?? "";
                var match = System.Text.RegularExpressions.Regex.Match(href, @"pagination_data=%5B(\d+)%5D");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                {
                    teamIds.Add(id);
                }
            }
        }

        // Strategy 3: Look for team names and try to extract IDs from nearby elements
        if (teamIds.Count == 0)
        {
            Console.WriteLine("  No teams found via standard patterns. Checking page structure...");
            var preview = html.Substring(0, Math.Min(2000, html.Length));
            Console.WriteLine($"  HTML preview:\n{preview}\n");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR parsing team IDs: {ex.Message}");
    }

    return teamIds.Distinct().ToList();
}

// --- FEST ingestion helper ---
static async Task RunFestIngestion(string[] args, Microsoft.Extensions.Configuration.IConfiguration config)
{
    Console.WriteLine("=== FEST Ingestion Mode ===");

    // Collect args or prompt interactively
    string festUrl = args.Length > 1 ? args[1] : string.Empty;
    string sessionToken = args.Length > 2 ? args[2] : string.Empty;
    string? azureToken = args.Length > 3 ? args[3] : null;

    if (string.IsNullOrEmpty(festUrl))
    {
        Console.Write("Paste the FEST get_matches URL (open_page=0): ");
        festUrl = Console.ReadLine()?.Trim() ?? string.Empty;
    }
    if (string.IsNullOrEmpty(sessionToken))
    {
        Console.Write("Paste the Modular11 session token (_token value): ");
        sessionToken = Console.ReadLine()?.Trim() ?? string.Empty;
    }
    if (string.IsNullOrEmpty(azureToken))
    {
        azureToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
    }

    if (string.IsNullOrEmpty(festUrl) || string.IsNullOrEmpty(sessionToken))
    {
        Console.WriteLine("ERROR: FEST URL and session token are required.");
        return;
    }

    // Extract tournament ID from URL (e.g. "tournament=75")
    var tournamentMatch = System.Text.RegularExpressions.Regex.Match(festUrl, @"[?&]tournament=(\d+)");
    var tournamentId = tournamentMatch.Success ? tournamentMatch.Groups[1].Value : "0";
    Console.WriteLine($"Detected tournament ID: {tournamentId}");

    var connectionString = config.GetConnectionString("DefaultConnection");

    Action<DbContextOptionsBuilder> configureDb = options =>
    {
        if (!string.IsNullOrEmpty(azureToken))
        {
            var connection = new SqlConnection(
                "Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
            connection.AccessToken = azureToken;
            options.UseSqlServer(connection);
        }
        else
        {
            options.UseSqlServer(connectionString);
        }
    };

    var settings = new Modular11Settings
    {
        TournamentId = tournamentId,
        Gender = "1",
        Status = "scheduled",
        MatchType = "1",
        AgeGroups = new List<string>(),
        SessionToken = sessionToken,
        BaseUrlOverride = festUrl,
    };

    var services = new ServiceCollection();
    services.AddHttpClient<Modular11Client>();
    services.AddScoped<ScheduleParser>();
    services.AddScoped<MatchUpsertService>();
    services.AddScoped<IngestionOrchestrator>();
    services.AddDbContext<AppDbContext>(configureDb);
    services.AddSingleton(settings);
    services.AddLogging(b => { b.AddConsole(); b.SetMinimumLevel(LogLevel.Information); });

    var sp = services.BuildServiceProvider();
    var orchestrator = sp.GetRequiredService<IngestionOrchestrator>();

    try
    {
        await orchestrator.RunAsync(CancellationToken.None, maxMatches: null, "MLS Next", startPage: 0);
        Console.WriteLine("✅ FEST ingestion complete.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FEST ingestion failed: {ex.Message}");
    }
    finally
    {
        await sp.DisposeAsync();
    }
}
