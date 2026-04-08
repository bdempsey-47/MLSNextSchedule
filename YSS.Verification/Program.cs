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

    Console.WriteLine($"\nDiscovering groups and teams from Modular11...");

    // Step 1: Fetch and parse get_teams to discover groups and team IDs
    string teamsUrl = "https://www.modular11.com/events/event/get_teams?tournament_type=event&UID_age=22&UID_gender=1&UID_event=84&list_type=groupplay&open_page=1";

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
        Console.WriteLine($"ERROR fetching get_teams: {ex.Message}");
        return;
    }

    var teamsHtml = await teamsResponse.Content.ReadAsStringAsync();
    var teamIds = ExtractTeamIdsFromGroupPlayHtml(teamsHtml);

    if (teamIds.Count == 0)
    {
        Console.WriteLine("ERROR: No teams found in get_teams response.");
        Console.WriteLine($"HTML preview (first 2000 chars): {teamsHtml.Substring(0, Math.Min(2000, teamsHtml.Length))}");
        return;
    }

    Console.WriteLine($"✓ Discovered {teamIds.Count} teams");

    // Step 2: Fetch matches for each team and accumulate
    var allParsedMatches = new List<ParsedMatch>();
    var seenMatchIds = new HashSet<string>();
    var parser = new ScheduleParser(LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ScheduleParser>());

    foreach (var teamId in teamIds)
    {
        // Build URL for this team: pagination_data=[teamId], bracket, group from the get_teams discovery
        // For simplicity, we use a generic call and let Modular11 handle pagination
        string matchUrl = $"https://www.modular11.com/events/event/get_partial_matches_by_team?open_page=1&pagination_data=%5B{teamId}%5D&bracket=39&age=22&tournament=84&group=1&list_type=groupplay";

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

            // Override division to NJ Cup Qualifier
            foreach (var match in parsedMatches)
            {
                match.Division = divisionName;
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

    Console.WriteLine($"\n✓ Collected {allParsedMatches.Count} total matches\n");

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

        // Try to find team rows via .form_row.main_row[js-group] or similar
        var teamRows = document.QuerySelectorAll(".form_row.main_row");

        foreach (var row in teamRows)
        {
            // Try to extract team ID from data-id, data-team-id, or href
            var dataId = row.GetAttribute("data-id");
            if (!string.IsNullOrEmpty(dataId) && int.TryParse(dataId, out var id))
            {
                teamIds.Add(id);
                continue;
            }

            var dataTeamId = row.GetAttribute("data-team-id");
            if (!string.IsNullOrEmpty(dataTeamId) && int.TryParse(dataTeamId, out var id2))
            {
                teamIds.Add(id2);
                continue;
            }

            // Try to find href with team ID pattern
            var link = row.QuerySelector("a[href*='/team/']");
            if (link != null)
            {
                var href = link.GetAttribute("href") ?? "";
                var match = System.Text.RegularExpressions.Regex.Match(href, @"/team/(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id3))
                {
                    teamIds.Add(id3);
                }
            }
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
