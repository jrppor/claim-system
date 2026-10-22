using Claim.Api.Auth;
using Claim.Api.Http;
using Claim.Application.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Claims;

/// <summary>
/// Claim Photos of one Claim. Uploading needs Garage Staff and the Claim version (If-Match); reading is open to every signed-in
/// role that may see the Claim. Photos have no public URL: the bytes come only from the file endpoint, behind these checks.
/// </summary>
[ApiController]
[Route("claims/{id:long}/photos")]
[Tags("Claim Photos")]
public sealed class ClaimPhotosController(ClaimPhotoService photos) : ControllerBase
{
    /// <summary>A little above the 10 MB per-file rule (form overhead), so an oversized file gets the readable 400, not a bare 413.</summary>
    private const long MaxUploadBytes = 12 * 1024 * 1024;

    [HttpPost]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload(long id, IFormFile file, CancellationToken ct)
    {
        var actor = Actor.From(User);
        await using var content = file.OpenReadStream();
        var photo = await photos.UploadAsync(id, file.FileName, file.Length, content, actor.UserId, actor.Role, Request.RequiredVersion(), ct);
        return photo is null ? this.ClaimNotFound(id) : Created($"/claims/{id}/photos/{photo.Id}/file", photo);
    }

    /// <summary>Removes one photo (row, then file). Garage Staff only; the answer carries the Claim's new version.</summary>
    [HttpDelete("{photoId:long}")]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> Delete(long id, long photoId, [FromServices] ClaimPhotoRemovalService removal, CancellationToken ct)
    {
        var actor = Actor.From(User);
        var version = await removal.RemoveAsync(id, photoId, actor.UserId, actor.Role, Request.RequiredVersion(), ct);
        return version is null
            ? this.ProblemResult("Not Found", $"Claim {id} has no photo {photoId}.", StatusCodes.Status404NotFound)
            : Ok(new { version });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(long id, CancellationToken ct)
    {
        var list = await photos.ListAsync(id, Actor.From(User).Role, ct);
        return list is null ? this.ClaimNotFound(id) : Ok(list);
    }

    [HttpGet("{photoId:long}/file")]
    [Authorize]
    public async Task<IActionResult> GetFile(long id, long photoId, CancellationToken ct)
    {
        var file = await photos.OpenAsync(id, photoId, Actor.From(User).Role, ct);
        if (file is null) return this.ProblemResult("Not Found", $"Claim {id} has no photo {photoId}.", StatusCodes.Status404NotFound);
        // The type was verified from the bytes at upload; nosniff stops a browser from guessing a different one.
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, file.ContentType);
    }
}
