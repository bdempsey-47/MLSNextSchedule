using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YSS.Data;
using YSS.Data.Entities;

namespace YSS.Ingestion.Services;

/// <summary>
/// Orchestrates the entire ingestion process:
/// - Pagination through Modular11 API
/// - HTML parsing and extraction
/// - Database upsert with deduplication
/// </summary>
public class IngestionOrchestrator
{
    private readonly Modular11Client _client;
    private readonly ScheduleParser _parser;
    private readonly MatchUpsertService _upsertService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<IngestionOrchestrator> _logger;

    public IngestionOrchestrator(
        Modular11Client client,
        ScheduleParser parser,
        MatchUpsertService upsertService,
        AppDbContext dbContext,
        ILogger<IngestionOrchestrator> logger)
    {
        _client = client;
        _parser = parser;
        _upsertService = upsertService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Run the complete ingestion job.
    /// Handles pagination, parsing, and database upsert.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="maxMatches">Optional cap on total matches to ingest. Null = no limit.</param>
    /// <param name="leagueName">Name of the league to ingest (e.g., 'MLS Next', 'ECNL', 'EDP'). Defaults to 'MLS Next'.</param>
    /// <param name="startDate">Optional start date override (yyyy-MM-dd HH:mm:ss). Overrides Modular11Settings when provided.</param>
    /// <param name="endDate">Optional end date override (yyyy-MM-dd HH:mm:ss). Overrides Modular11Settings when provided.</param>
    public async Task RunAsync(
        CancellationToken ct = default,
        int? maxMatches = null,
        string leagueName = "MLS Next",
        string? startDate = null,
        string? endDate = null,
        List<string>? ageGroups = null)
    {
        var startTime = DateTime.UtcNow;
        var totalMatches = 0;
        var pageNumber = 1;
        var seenMatchIds = new HashSet<string>();
        var noDataResponseCount = 0;

        _logger.LogInformation("Starting ingestion job");

        try
        {
            while (true)
            {
                _logger.LogInformation("Fetching page {PageNumber}", pageNumber);

                // Fetch page
                var htmlContent = await _client.FetchPageAsync(pageNumber, ct, startDate, endDate, ageGroups);

                // Check for end-of-pagination marker
                if (htmlContent.Contains("No data available"))
                {
                    _logger.LogInformation("Reached end of pagination at page {PageNumber}", pageNumber);
                    break;
                }

                // Parse matches from HTML
                var parsedMatches = _parser.ParseMatches(htmlContent, _client.TournamentId);

                // In-memory deduplication
                var newMatches = new List<Models.ParsedMatch>();
                foreach (var match in parsedMatches)
                {
                    if (!seenMatchIds.Contains(match.MatchId))
                    {
                        seenMatchIds.Add(match.MatchId);
                        newMatches.Add(match);
                    }
                    else
                    {
                        _logger.LogDebug("Skipping duplicate match ID: {MatchId}", match.MatchId);
                    }
                }

                // Stop after 3 consecutive pages with no new matches (catches both empty
                // responses and APIs that loop/repeat pages after the last real page)
                if (newMatches.Count == 0)
                {
                    noDataResponseCount++;
                    _logger.LogWarning("Page {PageNumber} returned no new matches (consecutive: {EmptyCount})", pageNumber, noDataResponseCount);

                    if (noDataResponseCount >= 3)
                    {
                        _logger.LogInformation("Stopping after {Count} consecutive pages with no new matches", noDataResponseCount);
                        break;
                    }
                }
                else
                {
                    noDataResponseCount = 0;
                }

                // Trim to respect MaxMatches cap
                if (maxMatches.HasValue)
                {
                    var remaining = maxMatches.Value - totalMatches;
                    if (newMatches.Count > remaining)
                        newMatches = newMatches.Take(remaining).ToList();
                }

                // Upsert to database
                if (newMatches.Count > 0)
                {
                    await _upsertService.UpsertMatchesAsync(newMatches, leagueName, ct);
                    totalMatches += newMatches.Count;
                    _logger.LogInformation("Total matches ingested so far: {Total}", totalMatches);
                }

                // Stop if we've hit the cap
                if (maxMatches.HasValue && totalMatches >= maxMatches.Value)
                {
                    _logger.LogInformation("Reached MaxMatches cap of {Max}. Stopping.", maxMatches.Value);
                    break;
                }

                pageNumber++;
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Ingestion job completed successfully. Total matches processed: {Total}, Duration: {Duration}",
                totalMatches, duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during ingestion job");
            throw;
        }
    }
}
