namespace Claim.Application.Claims;

/// <summary>
/// The thumbnail of each Claim Photo: made at upload, kept in IFileStorage beside the original, served under the same visibility
/// rules as the full photo, and removed with it. The upload path and ticket "delete photo" call this; nothing else knows how
/// thumbnails are named or built.
/// </summary>
public sealed class PhotoThumbnails(
    IThumbnailGenerator generator, IFileStorage storage, IClaimRepository claims, IClaimPhotoRepository photos)
{
    /// <summary>
    /// Builds and stores the thumbnail of an image whose type is already verified. Returns the storage name to record on the photo,
    /// or null when the bytes are not a decodable image (nothing is stored then). Leaves the stream position unspecified.
    /// </summary>
    public async Task<string?> StoreAsync(Stream image, string contentType, CancellationToken ct)
    {
        var bytes = await generator.CreateAsync(image, contentType, ct);
        if (bytes is null) return null;
        var name = Guid.NewGuid().ToString("N") + "_thumb" + Claim.Domain.Claims.PhotoFormat.Extension(contentType);
        await storage.SaveAsync(name, new MemoryStream(bytes), ct);
        return name;
    }

    /// <summary>Removes a stored thumbnail by name (also the clean-up when an upload fails after the thumbnail was stored). A missing file is fine.</summary>
    public Task RemoveAsync(string? thumbnailStoredName, CancellationToken ct) =>
        thumbnailStoredName is null ? Task.CompletedTask : storage.DeleteAsync(thumbnailStoredName, ct);

    /// <summary>The thumbnail bytes. Null when the Claim or photo does not exist, the role may not see them, or the photo has no thumbnail.</summary>
    public async Task<PhotoFile?> OpenAsync(long claimId, long photoId, string role, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(claimId, ct);
        if (claim is null || !claim.IsVisibleTo(role)) return null;
        var photo = (await photos.ListAsync(claimId, ct)).FirstOrDefault(p => p.Id == photoId);
        if (photo?.ThumbnailStoredName is null) return null;
        var stream = await storage.OpenReadAsync(photo.ThumbnailStoredName, ct);
        return stream is null ? null : new PhotoFile(stream, photo.ContentType);
    }
}
