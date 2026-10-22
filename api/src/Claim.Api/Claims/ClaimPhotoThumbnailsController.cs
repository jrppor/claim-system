using Claim.Api.Auth;
using Claim.Api.Http;
using Claim.Application.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Claims;

/// <summary>The 320 px thumbnail of a Claim Photo, behind the same sign-in and Claim visibility checks as the full photo.</summary>
[ApiController]
[Route("claims/{id:long}/photos")]
[Tags("Claim Photos")]
public sealed class ClaimPhotoThumbnailsController(PhotoThumbnails thumbnails) : ControllerBase
{
    [HttpGet("{photoId:long}/thumbnail")]
    [Authorize]
    public async Task<IActionResult> GetThumbnail(long id, long photoId, CancellationToken ct)
    {
        var file = await thumbnails.OpenAsync(id, photoId, Actor.From(User).Role, ct);
        if (file is null) return this.ProblemResult("Not Found", $"Claim {id} has no thumbnail for photo {photoId}.", StatusCodes.Status404NotFound);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, file.ContentType);
    }
}
