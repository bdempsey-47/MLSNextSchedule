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

// If an access token is provided as a CLI argument, set it as an environment variable
if (args.Length > 0)
{
    Environment.SetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN", args[0]);
    Console.WriteLine("Using Azure SQL with access token authentication");
}

var connectionString = config.GetConnectionString("DefaultConnection");
var accessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
const int MaxMatchesPerTournament = 25;
var dbInfo = !string.IsNullOrEmpty(accessToken) ? "Azure SQL (token auth)" : connectionString;
Console.WriteLine($"=== MLSNext Full Ingestion Runner ===\nDB: {dbInfo}\nLimit: {MaxMatchesPerTournament} records per tournament\n");

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

// Clear database before ingestion
Console.WriteLine("Clearing database...");
{
    var services = new ServiceCollection();
    services.AddDbContext<AppDbContext>(ConfigureDbContext);
    var sp = services.BuildServiceProvider();
    var db = sp.GetRequiredService<AppDbContext>();
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Matches");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM RawIngestionLogs");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Regions");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Divisions");
    // Don't delete Leagues — keep the seeded league records
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Teams");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Venues");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Competitions");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM AgeGroups");
    Console.WriteLine("Database cleared (Leagues table preserved)\n");
    await sp.DisposeAsync();
}

// Run ingestion for each tournament
var tournaments = new[]
{
    new { TournamentId = "35", Label = "Academy",    StartDate = "2025-07-01 00:00:01", EndDate = "2025-12-31 23:59:59" },
    new { TournamentId = "12", Label = "Homegrown",  StartDate = "2025-07-01 00:00:01", EndDate = "2025-12-31 23:59:59" },
    new { TournamentId = "35", Label = "Academy S26", StartDate = "2026-01-01 00:00:01", EndDate = "2026-06-30 23:59:59" },
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
        AgeGroups = new List<string> { "13", "14", "15", "16", "17", "18" },
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
        await orchestrator.RunAsync(CancellationToken.None, MaxMatchesPerTournament, "MLS Next");
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

