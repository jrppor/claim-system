using Claim.Application.Health;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Http;

[ApiController]
[Route("health")]
[Tags("System")]
public sealed class HealthController(HealthService health) : ControllerBase
{
    [HttpGet(Name = "Health")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var report = await health.CheckAsync(ct);
        return report.IsHealthy
            ? Ok(report)
            : this.ProblemResult("Database unavailable", null, StatusCodes.Status503ServiceUnavailable);
    }
}
