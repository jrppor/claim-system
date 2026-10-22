using Claim.Api.Auth;
using Claim.Api.Http;
using Claim.Application.Claims;
using Claim.Domain.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Claims;

/// <summary>
/// Claim Photos of one Claim. Uploading needs Garage Staff and the Claim version (If-Match); reading is open to every signed-in
/// role that may see the Claim. Photos have no public URL: the bytes come only from the file endpoints, behind these checks.
/// </summary>
[ApiController]
[Route("claims/{id:long}/photos")]
[Tags("Claim Photos")]
public sealed class ClaimPhotosController(ClaimPhotoService photos) : ControllerBase
{
    /// <summary>
    /// The most a legitimate request carries: a full 20 files of 10 MB plus a little for multipart framing and captions. Kept
    /// tight on purpose; anything bigger is answered 413 before it is buffered.
    /// </summary>
    public const long MaxUploadBytes = PhotoFormat.MaxPerClaim * PhotoFormat.MaxBytes + 1024 * 1024;

    /// <summary>
    /// One or more files (form field "file", repeated) with optional captions (field "caption", repeated, paired by position).
    /// The photos in the answer carry the Claim's new version.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    [UploadSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload(long id, [FromForm(Name = "file")] List<IFormFile> files, CancellationToken ct)
    {
        var actor = Actor.From(User);
        var incoming = files.Select(f => new IncomingPhoto(f.FileName, f.Length, f.OpenReadStream)).ToList();
        var captions = Request.Form["caption"].Select(c => (string?)c).ToList();
        var created = await photos.UploadAsync(id, incoming, captions, actor.UserId, actor.Role, Request.RequiredVersion(), ct);
        return created is null ? this.ClaimNotFound(id) : Created($"/claims/{id}/photos", UploadResponseBody(files.Count, created));
    }

    /// <summary>
    /// The one place that decides the shape of an upload answer: a request that sent a single file gets that photo as an object,
    /// a request that sent several gets a list. This keeps the original single-file contract (the first release returned one
    /// object) while multi-file clients get a list. It follows what the client sent, not what was stored, so the shape never
    /// depends on the Claim's state.
    /// </summary>
    private static object UploadResponseBody(int filesSent, IReadOnlyList<ClaimPhotoDto> created) =>
        filesSent == 1 ? created[0] : created;

    /// <summary>Removes one photo (row, then files). Garage Staff only; the answer carries the Claim's new version.</summary>
    [HttpDelete("{photoId:long}")]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> Delete(long id, long photoId, [FromServices] ClaimPhotoRemovalService removal, CancellationToken ct)
    {
        var actor = Actor.From(User);
        var version = await removal.RemoveAsync(id, photoId, actor.UserId, actor.Role, Request.RequiredVersion(), ct);
        return version is null ? PhotoNotFound(id, photoId) : Ok(new { version });
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
    public async Task<IActionResult> GetFile(long id, long photoId, CancellationToken ct) =>
        Image(await photos.OpenAsync(id, photoId, Actor.From(User).Role, PhotoVariant.Original, ct), id, photoId);

    /// <summary>The 320 px thumbnail, behind the same sign-in and Claim visibility checks as the full photo.</summary>
    [HttpGet("{photoId:long}/thumbnail")]
    [Authorize]
    public async Task<IActionResult> GetThumbnail(long id, long photoId, CancellationToken ct) =>
        Image(await photos.OpenAsync(id, photoId, Actor.From(User).Role, PhotoVariant.Thumbnail, ct), id, photoId);

    /// <summary>
    /// The bytes of a photo file. The type was verified from the bytes at upload; nosniff stops a browser from guessing another.
    /// Evidence is private to signed-in users, so no cache (browser or shared) may keep a copy.
    /// </summary>
    private IActionResult Image(PhotoFile? file, long claimId, long photoId)
    {
        if (file is null) return PhotoNotFound(claimId, photoId);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(file.Content, file.ContentType);
    }

    private IActionResult PhotoNotFound(long claimId, long photoId) =>
        this.ProblemResult("Not Found", $"Claim {claimId} has no photo {photoId}.", StatusCodes.Status404NotFound);
}
