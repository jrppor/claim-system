using Claim.Domain.Claims;
using ClaimEntity = Claim.Domain.Claims.Claim;

namespace Claim.Application.Claims;

/// <summary>Claim Photos: attaching and reading damage evidence (ADR 0006). The Claim's status never changes here.</summary>
public sealed class ClaimPhotoService(
    IClaimRepository claims,
    IClaimPhotoRepository photos,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    PhotoThumbnails thumbnails,
    TimeProvider clock)
{
    /// <summary>
    /// Stores the file, then the row and its Claim History line in one transaction; if that fails the file is removed again, so
    /// neither an orphan file nor a half-saved row is left. Returns null when the Claim does not exist or the role may not see it.
    /// </summary>
    public async Task<ClaimPhotoDto?> UploadAsync(
        long claimId, string fileName, long length, Stream content, long actorUserId, string actorRole, byte[] expectedVersion, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(claimId, ct);
        if (claim is null || !claim.IsVisibleTo(actorRole)) return null;
        claim.EnsureVersion(expectedVersion);

        claim.EnsureAcceptsPhotoChange();

        fileName = PhotoFormat.DisplayName(fileName);
        if (length > PhotoFormat.MaxBytes)
            throw FileRejected($"{fileName}: the file is larger than {PhotoFormat.MaxBytes / (1024 * 1024)} MB.");

        var header = new byte[PhotoFormat.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var contentType = PhotoFormat.Detect(header.AsSpan(0, read));
        if (contentType is null)
            throw FileRejected($"{fileName}: only JPEG, PNG and WebP images are accepted.");
        content.Position = 0;

        var thumbnailName = await thumbnails.StoreAsync(content, contentType, ct);
        if (thumbnailName is null)
            throw FileRejected($"{fileName}: the file is not a readable image.");
        content.Position = 0;

        var storedName = Guid.NewGuid().ToString("N") + PhotoFormat.Extension(contentType);
        var now = clock.GetUtcNow().UtcDateTime;
        var draft = new ClaimPhoto(claimId, fileName, storedName, contentType, length, null, actorUserId, now) { ThumbnailStoredName = thumbnailName };
        var entry = claim.RecordPhotoAdded(fileName, actorUserId, now);

        try
        {
            await storage.SaveAsync(storedName, content, ct);
            var stored = await unitOfWork.RecordPhotoAsync(draft, claim, entry, ct);
            return ToDto(stored, claim);
        }
        catch
        {
            await storage.DeleteAsync(storedName, CancellationToken.None);
            await thumbnails.RemoveAsync(thumbnailName, CancellationToken.None);
            throw;
        }
    }

    /// <summary>Photos of a Claim, oldest first. Null when the Claim does not exist or the role may not see it.</summary>
    public async Task<IReadOnlyList<ClaimPhotoDto>?> ListAsync(long claimId, string role, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(claimId, ct);
        if (claim is null || !claim.IsVisibleTo(role)) return null;
        return (await photos.ListAsync(claimId, ct)).Select(p => ToDto(p, claim)).ToList();
    }

    /// <summary>The stored bytes of one photo. Null when the Claim or photo does not exist or the role may not see them.</summary>
    public async Task<PhotoFile?> OpenAsync(long claimId, long photoId, string role, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(claimId, ct);
        if (claim is null || !claim.IsVisibleTo(role)) return null;
        var photo = (await photos.ListAsync(claimId, ct)).FirstOrDefault(p => p.Id == photoId);
        if (photo is null) return null;
        var stream = await storage.OpenReadAsync(photo.StoredName, ct);
        return stream is null ? null : new PhotoFile(stream, photo.ContentType);
    }

    private static ClaimValidationException FileRejected(string reason) =>
        new(new Dictionary<string, string[]> { ["file"] = [reason] });

    private static ClaimPhotoDto ToDto(ClaimPhoto p, ClaimEntity claim) => new(
        p.Id, p.ClaimId, p.OriginalFileName, p.ContentType, p.SizeBytes, p.Caption, p.UploadedByUserId, p.UploadedAt,
        ClaimVersion.Format(claim.RowVersion));
}
