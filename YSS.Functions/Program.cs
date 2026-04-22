using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YSS.Data;
using YSS.Ingestion.Services;
using YSS.Functions.Models;
using YSS.Functions.Triggers;
using YSS.Constants;

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

        services.AddHttpClient<SearchTeams>();

        // Register ingestion services
        var modular11Config = config.GetSection("Modular11");
        
        // Parse age group codes from config
        // Modular11 API age group codes (see YSS.Constants.AgeGroupConstants for mapping):
        //   21=U13, 22=U14, 33=U15, 14=U16, 15=U17, 26=U19
        // Example config: "AgeGroups": "21,22,33,14,15,26"
        var defaultAgeGroups = $"{AgeGroupConstants.U13Code},{AgeGroupConstants.U14Code},{AgeGroupConstants.U15Code},{AgeGroupConstants.U16Code},{AgeGroupConstants.U17Code},{AgeGroupConstants.U19Code}";
        var ageGroupsStr = modular11Config["AgeGroups"] ?? defaultAgeGroups;
        var ageGroups = ageGroupsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();

        var settings = new Modular11Settings
        {
            TournamentId = modular11Config["TournamentId"] ?? TournamentConstants.AcademyTournamentId,
            Gender = modular11Config["Gender"] ?? Modular11ApiConstants.GenderMale,
            Status = modular11Config["Status"] ?? Modular11ApiConstants.StatusScheduled,
            MatchType = modular11Config["MatchType"] ?? Modular11ApiConstants.MatchType,
            AgeGroups = ageGroups,
            StartDate = modular11Config["StartDate"],
            EndDate = modular11Config["EndDate"]
        };

        services.AddSingleton(settings);

        // Register active tournament seasons — add new seasons here each year
        var seasons = new List<TournamentSeason>
        {
            new(TournamentConstants.AcademyTournamentId, "Academy S26",   "MLS Next", new DateTime(2026, 1, 1), new DateTime(2026, 6, 30)),
            new(TournamentConstants.HomegrownTournamentId, "Homegrown S26", "MLS Next", new DateTime(2026, 1, 1), new DateTime(2026, 6, 30)),
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
        services.AddScoped<GetHomepageStats>();
        services.AddScoped<ComputeHomepageSnapshot>();
        services.AddScoped<YSS.Functions.Services.EloRecomputeService>();
        services.AddScoped<YSS.Functions.Services.HomepageSnapshotService>();

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

