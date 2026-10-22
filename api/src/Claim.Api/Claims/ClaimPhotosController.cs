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
    /// <summary>Room for the most a request can legitimately carry: 20 photos of 10 MB plus form overhead.</summary>
    private const long MaxUploadBytes = 21L * 10 * 1024 * 1024 + 2 * 1024 * 1024;

    /// <summary>
    /// One or more files (form field "file", repeated). A request with a single file answers with that photo, several answer with
    /// the list of photos; either way the photos carry the Claim's new version.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload(long id, [FromForm(Name = "file")] List<IFormFile> files, CancellationToken ct)
    {
        var actor = Actor.From(User);
        var uploads = new List<PhotoUpload>();
        try
        {
            foreach (var file in files) uploads.Add(new PhotoUpload(file.FileName, file.Length, file.OpenReadStream(), null));
            var created = await photos.UploadAsync(id, uploads, actor.UserId, actor.Role, Request.RequiredVersion(), ct);
            if (created is null) return this.ClaimNotFound(id);
            return Created($"/claims/{id}/photos", files.Count == 1 ? created[0] : created);
        }
        finally
        {
            foreach (var upload in uploads) await upload.Content.DisposeAsync();
        }
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
