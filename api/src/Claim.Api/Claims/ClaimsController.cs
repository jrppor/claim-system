using Claim.Api.Auth;
using Claim.Api.Http;
using Claim.Application.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Claims;

[ApiController]
[Route("claims")]
[Tags("Claims")]
public sealed class ClaimsController(ClaimService service) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> CreateDraft(ClaimInput input, CancellationToken ct)
    {
        var created = await service.CreateDraftAsync(input, Actor.From(User).UserId, ct);
        return Created($"/claims/{created.Id}", created);
    }

    [HttpGet("{id:long}")]
    [Authorize]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var claim = await service.GetAsync(id, Actor.From(User).Role, ct);
        return claim is null ? this.ClaimNotFound(id) : Ok(claim);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> EditDraft(long id, ClaimInput input, CancellationToken ct)
    {
        var claim = await service.EditDraftAsync(id, input, Request.RequiredVersion(), ct);
        return claim is null ? this.ClaimNotFound(id) : Ok(claim);
    }
}
