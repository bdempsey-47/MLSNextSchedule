using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MLSNext.Data;
using Microsoft.EntityFrameworkCore;

namespace MLSNext.Functions.Triggers;

public class GetAgeGroups
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public GetAgeGroups(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<GetAgeGroups>();
    }

    [Function("GetAgeGroups")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "agegroups")] HttpRequestData req)
    {
        try
        {
            var ageGroups = await _context.AgeGroups
                .OrderBy(a => a.Name)
                .Select(a => new { a.Id, a.Name })
                .ToListAsync();

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ageGroups);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in GetAgeGroups: {ex.Message}");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}
