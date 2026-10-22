using Claim.Api.Auth;
using Claim.Application.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Claims;

[ApiController]
[Route("policies")]
[Tags("Policies")]
[Authorize(Policy = AuthPolicies.GarageStaff)]
public sealed class PoliciesController(PolicyService policies) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(string? q, CancellationToken ct) => Ok(await policies.SearchAsync(q, ct));
}
