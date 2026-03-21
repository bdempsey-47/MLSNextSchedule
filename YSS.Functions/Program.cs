using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YSS.Data;
using YSS.Ingestion.Services;
using YSS.Functions.Models;
using YSS.Functions.Triggers;

var host = new HostBuilder()
    .ConfigureServices(services =>
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Register DbContext
        var connectionString = config.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Register ingestion services
        var modular11Config = config.GetSection("Modular11");
        var ageGroupsStr = modular11Config["AgeGroups"] ?? "";
        var ageGroups = ageGroupsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();

        var settings = new Modular11Settings
        {
            TournamentId = modular11Config["TournamentId"] ?? "35",
            Gender = modular11Config["Gender"] ?? "1",
            Status = modular11Config["Status"] ?? "scheduled",
            MatchType = modular11Config["MatchType"] ?? "2",
            AgeGroups = ageGroups,
            StartDate = modular11Config["StartDate"],
            EndDate = modular11Config["EndDate"]
        };

        services.AddSingleton(settings);

        // Register active tournament seasons — add new seasons here each year
        var seasons = new List<TournamentSeason>
        {
            new("35", "Academy S26",   "MLS Next", new DateTime(2026, 1, 1), new DateTime(2026, 6, 30)),
            new("12", "Homegrown S26", "MLS Next", new DateTime(2026, 1, 1), new DateTime(2026, 6, 30)),
        };
        services.AddSingleton(seasons);

        services.AddScoped<Modular11Client>();
        services.AddScoped<ScheduleParser>();
        services.AddScoped<MatchUpsertService>();
        services.AddScoped<IngestionOrchestrator>();

        // Explicitly register function classes
        services.AddScoped<GetMatches>();
        services.AddScoped<GetTeams>();
        services.AddScoped<GetDivisions>();
        services.AddScoped<GetRegions>();
        services.AddScoped<GetAgeGroups>();
        services.AddScoped<GetStandings>();
        services.AddScoped<TriggerIngestion>();
        services.AddScoped<ScheduledIngestion>();
        services.AddScoped<WeeklyIngestion>();
        services.AddScoped<GetPowerRankings>();

        // Add HTTP client factory
        services.AddHttpClient<Modular11Client>();
        // Named client for GetStandings proxy — gzip decompression required for Modular11 responses
        services.AddHttpClient("standings")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });
    })
    .ConfigureFunctionsWorkerDefaults((Action<IFunctionsWorkerApplicationBuilder>)(builder => { }))
    .Build();

host.Run();

