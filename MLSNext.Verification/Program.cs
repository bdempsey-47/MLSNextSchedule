using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLSNext.Ingestion.Models;
using MLSNext.Ingestion.Services;

// Load configuration from local.settings.json
var configPath = Path.Combine(Directory.GetCurrentDirectory(), "local.settings.json");
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("local.settings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

// Extract Modular11 settings
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

Console.WriteLine("=== MLSNext Schedule Data Verification ===\n");
Console.WriteLine($"API Configuration:");
Console.WriteLine($"  Tournament ID: {settings.TournamentId}");
Console.WriteLine($"  Gender: {settings.Gender}");
Console.WriteLine($"  Age Groups: {string.Join(", ", settings.AgeGroups)}");
Console.WriteLine($"  Status: {settings.Status}");
Console.WriteLine($"  Match Type: {settings.MatchType}");
Console.WriteLine("\n" + new string('=', 60) + "\n");

// Set up dependency injection
var services = new ServiceCollection();
services.AddHttpClient<Modular11Client>();
services.AddScoped<ScheduleParser>();
services.AddSingleton(settings);

// Configure logging to see debug output
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var serviceProvider = services.BuildServiceProvider();
var client = serviceProvider.GetRequiredService<Modular11Client>();
var parser = serviceProvider.GetRequiredService<ScheduleParser>();

try
{
    Console.WriteLine("Fetching data from Modular11 API (Page 1)...\n");
    
    // Build URL for debugging
    var ageGroupStr = string.Join("&", settings.AgeGroups.Select(ag => $"age[]={ag}"));
    var dateParams = "";
    if (!string.IsNullOrEmpty(settings.StartDate))
        dateParams += $"&start_date={settings.StartDate}";
    if (!string.IsNullOrEmpty(settings.EndDate))
        dateParams += $"&end_date={settings.EndDate}";
    
    var debugUrl = $"https://www.modular11.com/public_schedule/league/get_matches?tournament={settings.TournamentId}&gender={settings.Gender}&status={settings.Status}&match_type={settings.MatchType}&open_page=1&{ageGroupStr}{dateParams}";
    
    Console.WriteLine($"Request URL: {debugUrl}\n");
    
    var html = await client.FetchPageAsync(pageNumber: 1, ct: CancellationToken.None);
    
    if (string.IsNullOrEmpty(html))
    {
        Console.WriteLine("❌ No data returned from API");
        return;
    }
    
    Console.WriteLine($"✅ Successfully retrieved HTML ({html.Length} bytes)\n");
    
    // Display raw HTML for debugging
    Console.WriteLine("Raw HTML Response:");
    Console.WriteLine(new string('-', 60));
    Console.WriteLine(html);
    Console.WriteLine(new string('-', 60));
    Console.WriteLine();
    
    Console.WriteLine("Parsing matches from HTML...\n");
    var matches = parser.ParseMatches(html);
    
    if (!matches.Any())
    {
        Console.WriteLine("❌ No matches found in parsed data");
        return;
    }
    
    Console.WriteLine($"✅ Found {matches.Count} match(es)\n");
    Console.WriteLine(new string('=', 60) + "\n");
    
    // Display each match with all details
    foreach ((var match, int index) in matches.Select((m, i) => (m, i + 1)))
    {
        Console.WriteLine($"MATCH {index}:");
        Console.WriteLine($"  Match ID:     {match.MatchId}");
        Console.WriteLine($"  Date (UTC):   {match.MatchDate:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  Home Team:    {match.HomeTeamName}");
        Console.WriteLine($"  Away Team:    {match.AwayTeamName}");
        Console.WriteLine($"  Venue:        {match.VenueName}");
        Console.WriteLine($"  Division:     {match.Division}");
        Console.WriteLine($"  Age Group:    {match.AgeGroup}");
        Console.WriteLine($"  Competition:  {match.Competition}");
        Console.WriteLine($"  Gender:       {match.Gender}");
        Console.WriteLine($"  Score:        {match.Score}");
        Console.WriteLine();
    }
    
    Console.WriteLine(new string('=', 60));
    Console.WriteLine($"\n✅ Data verification complete. {matches.Count} match(es) successfully retrieved and parsed.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error during data verification:");
    Console.WriteLine($"   {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
}
