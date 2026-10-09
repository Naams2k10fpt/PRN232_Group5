using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QuizManagement.Controllers.Api;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class HealthApiController(HealthCheckService healthChecks) : ControllerBase
{
    /// <summary>Checks the application and database, without requiring a token.</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var statusCode = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;
        return StatusCode(statusCode, new HealthResponse(report.Status.ToString()));
    }
}

public sealed record HealthResponse(string Status);
