using Claim.Api.Http;
using Claim.Application.Claims;
using Claim.Domain.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Claims;

/// <summary>Read-only endpoints: Claim list and dashboard summary. Open to every authenticated role; visibility is applied by role.</summary>
[ApiController]
[Authorize]
public sealed class ClaimListController(ClaimListService service) : ControllerBase
{
    [HttpGet("claims")]
    [Tags("Claims")]
    public async Task<IActionResult> List(string? search, string? status, int? page, int? pageSize, CancellationToken ct)
    {
        ClaimStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ClaimStatus>(status, ignoreCase: true, out var s) || !Enum.IsDefined(s))
                return this.ValidationError(new Dictionary<string, string[]> { ["status"] = [$"Unknown status '{status}'."] });
            parsed = s;
        }
        return Ok(await service.ListAsync(Actor.From(User).Role, search, parsed, page, pageSize, ct));
    }

    [HttpGet("dashboard/summary")]
    [Tags("Dashboard")]
    public async Task<IActionResult> Summary(CancellationToken ct) =>
        Ok(await service.SummaryAsync(Actor.From(User).Role, ct));
}
