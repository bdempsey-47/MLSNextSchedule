using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MLSNext.Data;
using Microsoft.EntityFrameworkCore;

namespace MLSNext.Functions.Triggers;

public class GetDivisions
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public GetDivisions(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<GetDivisions>();
    }

    [Function("GetDivisions")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "divisions")] HttpRequestData req)
    {
        try
        {
            var divisions = await _context.Divisions
                .OrderBy(d => d.Name)
                .Select(d => new { d.Id, d.Name })
                .ToListAsync();

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(divisions);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in GetDivisions: {ex.Message}");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}
