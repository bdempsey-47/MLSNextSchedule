using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MLSNext.Data;
using MLSNext.Ingestion.Services;

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
        services.AddScoped<Modular11Client>();
        services.AddScoped<ScheduleParser>();
        services.AddScoped<MatchUpsertService>();
        services.AddScoped<IngestionOrchestrator>();

        // Add HTTP client factory
        services.AddHttpClient<Modular11Client>();
    })
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();

