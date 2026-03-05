using System.Text;
using Microsoft.Extensions.Logging;

namespace YSS.Ingestion.Services;

/// <summary>
/// HTTP client for fetching schedule data from the Modular11 API.
/// Handles query parameter building, throttling, and pagination.
/// </summary>
public class Modular11Client
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<Modular11Client> _logger;
    private readonly Modular11Settings _settings;
    private const int MinThrottleMilliseconds = 1000;
    private const int MaxThrottleMilliseconds = 3000;

    public Modular11Client(HttpClient httpClient, ILogger<Modular11Client> logger, Modular11Settings settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;
    }

    /// <summary>
    /// Get the tournament ID for this client instance.
    /// </summary>
    public int TournamentId => int.TryParse(_settings.TournamentId, out var id) ? id : 0;

    /// <summary>
    /// Fetch a page of match data from Modular11.
    /// </summary>
    /// <param name="pageNumber">The 1-indexed page number</param>
    /// <param name="ct">Cancellation token</param>
    /// <param name="startDateOverride">Optional start date override (yyyy-MM-dd HH:mm:ss). Overrides settings when provided.</param>
    /// <param name="endDateOverride">Optional end date override (yyyy-MM-dd HH:mm:ss). Overrides settings when provided.</param>
    /// <returns>Raw HTML response body</returns>
    public virtual async Task<string> FetchPageAsync(int pageNumber, CancellationToken ct = default,
        string? startDateOverride = null, string? endDateOverride = null)
    {
        var throttleMs = Random.Shared.Next(MinThrottleMilliseconds, MaxThrottleMilliseconds + 1);
        _logger.LogDebug("Throttling request for page {PageNumber} by {ThrottleMs}ms", pageNumber, throttleMs);
        await Task.Delay(throttleMs, ct);

        var queryParams = BuildQueryParams(pageNumber, startDateOverride, endDateOverride);
        var url = $"https://www.modular11.com/public_schedule/league/get_matches?{queryParams}";

        _logger.LogInformation("Fetching Modular11 page {PageNumber}: {Url}", pageNumber, url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("Page {PageNumber} fetched successfully, response size: {Size} bytes", pageNumber, content.Length);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching page {PageNumber}", pageNumber);
            throw;
        }
    }

    private string BuildQueryParams(int pageNumber, string? startDateOverride = null, string? endDateOverride = null)
    {
        var sb = new StringBuilder();

        // Required parameters
        sb.Append($"tournament={Uri.EscapeDataString(_settings.TournamentId)}");
        sb.Append($"&gender={Uri.EscapeDataString(_settings.Gender)}");
        sb.Append($"&status={Uri.EscapeDataString(_settings.Status)}");
        sb.Append($"&match_type={Uri.EscapeDataString(_settings.MatchType)}");
        sb.Append($"&open_page={pageNumber}");

        // Age groups (repeatable parameter)
        foreach (var ageGroup in _settings.AgeGroups)
        {
            sb.Append($"&age[]={Uri.EscapeDataString(ageGroup)}");
        }

        // Date range — overrides take precedence over settings
        var startDate = startDateOverride ?? _settings.StartDate;
        var endDate = endDateOverride ?? _settings.EndDate;

        if (!string.IsNullOrEmpty(startDate))
            sb.Append($"&start_date={Uri.EscapeDataString(startDate)}");

        if (!string.IsNullOrEmpty(endDate))
            sb.Append($"&end_date={Uri.EscapeDataString(endDate)}");

        return sb.ToString();
    }
}

/// <summary>
/// Configuration settings for Modular11 API requests.
/// </summary>
public class Modular11Settings
{
    public required string TournamentId { get; set; }
    public required string Gender { get; set; }
    public required string Status { get; set; }
    public required string MatchType { get; set; }
    public required List<string> AgeGroups { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}
