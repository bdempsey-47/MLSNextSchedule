using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLSNext.Data;
using MLSNext.Data.Entities;

namespace MLSNext.Ingestion.Services;

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
    public async Task RunAsync(CancellationToken ct = default)
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
                var htmlContent = await _client.FetchPageAsync(pageNumber, ct);

                // Check for end-of-pagination marker
                if (htmlContent.Contains("No data available"))
                {
                    _logger.LogInformation("Reached end of pagination at page {PageNumber}", pageNumber);
                    break;
                }

                // Parse matches from HTML
                var parsedMatches = _parser.ParseMatches(htmlContent);

                if (parsedMatches.Count == 0)
                {
                    noDataResponseCount++;
                    _logger.LogWarning("Page {PageNumber} returned no matches (empty count: {EmptyCount})", pageNumber, noDataResponseCount);
                    
                    // Stop after 3 consecutive empty pages
                    if (noDataResponseCount >= 3)
                    {
                        _logger.LogInformation("Stopping after {Count} consecutive empty pages", noDataResponseCount);
                        break;
                    }
                }
                else
                {
                    noDataResponseCount = 0; // Reset counter
                }

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

                // Upsert to database
                if (newMatches.Count > 0)
                {
                    await _upsertService.UpsertMatchesAsync(newMatches, ct);
                    totalMatches += newMatches.Count;
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
