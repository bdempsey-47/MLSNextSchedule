using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

// --- FEST ingestion helper ---
static async Task RunFestIngestion(string[] args, IConfiguration config)
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
