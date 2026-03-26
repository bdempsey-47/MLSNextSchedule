using System.Net.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace YSS.Functions.Triggers
{
    public class SearchTeams
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SearchTeams> _logger;
        private readonly string _searchApiKey;
        private readonly string _searchServiceName = "yss-ai-search-prod";
        private readonly string _indexName = "search-teamnames";

        public SearchTeams(HttpClient httpClient, ILogger<SearchTeams> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _searchApiKey = config["AzureSearchApiKey"] ?? throw new InvalidOperationException("AzureSearchApiKey not configured");
        }

        [Function("SearchTeams")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "search-teams")] 
            HttpRequestData req)
        {
            _logger.LogInformation("SearchTeams function triggered");

            // Extract the query parmameter 'q' for the search term
            var searchQuery = req.Query["q"];
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                _logger.LogWarning("No search query provided");
                var emptyResponse = req.CreateResponse();
                await emptyResponse.WriteAsJsonAsync(new List<object>());
                return emptyResponse;
            }

            var query = searchQuery.ToString().Trim();
            var program = req.Query["program"];

            // Minimum 2 characters to avoid overly broad queries
            if (query.Length < 2)
            {
                _logger.LogDebug($"Search query too short: {query.Length} chars");
                var briefResponse = req.CreateResponse();
                await briefResponse.WriteAsJsonAsync(new List<object>());
                return briefResponse;
            }

            _logger.LogDebug($"Searching for: {query}");

            var searchRequest = new SearchRequest
            {
                Search = $"{query}*" // Wildcard search for partial matches
            };

            // Filter by program (AG=Academy, HG=Homegrown) if provided
            if (!string.IsNullOrWhiteSpace(program))
            {
                searchRequest.Filter = $"Program eq '{program}'";
            }

            var searchUrl = $"https://{_searchServiceName}.search.windows.net/indexes/{_indexName}/docs/search?api-version=2024-07-01";

            var requestJson = JsonSerializer.Serialize(searchRequest);
            var request = new HttpRequestMessage(HttpMethod.Post, searchUrl);
            request.Headers.Add("api-key", _searchApiKey);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            HttpResponseMessage searchResponse;
            try
            {
                searchResponse = await _httpClient.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Azure Search API call failed: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadGateway);
                await errorResponse.WriteAsJsonAsync(new { error = "Search service unavailable" });
                return errorResponse;
            }

            if (!searchResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"Azure Search returned {searchResponse.StatusCode}: {await searchResponse.Content.ReadAsStringAsync()}");
                var errorResponse = req.CreateResponse(searchResponse.StatusCode);
                await errorResponse.WriteAsJsonAsync(new { error = "Search failed" });
                return errorResponse;
            }

            var responseContent = await searchResponse.Content.ReadAsStringAsync();
            _logger.LogDebug($"Azure Search response: {responseContent}");

            // Parse the response
            var searchResult = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Extract the results array
            if (searchResult.TryGetProperty("value", out var resultsArray))
            {
                var teams = new List<object>();
                foreach (var item in resultsArray.EnumerateArray())
                {
                    //Extract just the fields you need (Id, Name, Program)
                    if (item.TryGetProperty("Id", out var id) &&
                        item.TryGetProperty("Name", out var name) &&
                        item.TryGetProperty("Program", out var program))
                    {
                        teams.Add(new
                        {
                            Id = id.GetString(),
                            Name = name.GetString(),
                            Program = program.GetString()
                        });
                    }
                }

                var finalResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await finalResponse.WriteAsJsonAsync(teams);
                return finalResponse;
            }
            // Add this fallback for when "value" property is missing
            _logger.LogError("Azure Search response missing 'value' property");
            var fallbackResponse = req.CreateResponse(System.Net.HttpStatusCode.BadGateway);
            await fallbackResponse.WriteAsJsonAsync(new { error = "Unexpected search response format" });
            return fallbackResponse;
        }

        private class SearchRequest
        {
            [JsonPropertyName("search")]
            public required string Search { get; set; }  

            [JsonPropertyName("queryType")]
            public string QueryType { get; set; } = "full"; //Enables wildcard and fuzzy search

            [JsonPropertyName("filter")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Filter { get; set; }
        }
    }

}