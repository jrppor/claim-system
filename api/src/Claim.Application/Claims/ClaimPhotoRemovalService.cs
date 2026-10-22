using Microsoft.Extensions.Logging;

namespace Claim.Application.Claims;

/// <summary>Deleting a Claim Photo (ADR 0006). The Claim's status never changes here.</summary>
public sealed class ClaimPhotoRemovalService(
    ClaimPhotoLookup lookup,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    TimeProvider clock,
    ILogger<ClaimPhotoRemovalService> logger)
{
    /// <summary>
    /// Removes the row and its Claim History line in one transaction, and only then the files: a failure before the commit leaves
    /// photo and files all intact; a failure of a file delete after the commit leaves only an unreferenced file, never a row
    /// pointing at a missing file. The original and the thumbnail are removed independently, so one failing never leaves the other
    /// behind. Returns the Claim's new version, or null when the Claim, photo or role's view does not exist.
    /// </summary>
    public async Task<string?> RemoveAsync(
        long claimId, long photoId, long actorUserId, string actorRole, byte[] expectedVersion, CancellationToken ct)
    {
        var found = await lookup.FindAsync(claimId, photoId, actorRole, ct);
        if (found is null) return null;
        var (claim, photo) = (found.Claim, found.Photo);
        claim.EnsureVersion(expectedVersion);

        var entry = claim.RecordPhotoRemoved(photo.OriginalFileName, actorUserId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.RecordPhotoRemovedAsync(photo, claim, entry, ct);

        // The deletion is already committed; answering with an error would invite a retry that can only 404. A leftover file is
        // unreferenced and harmless, but must be findable for cleanup, so each failure is logged with its own stored name.
        await TryDeleteAsync(photo.StoredName, claimId, photoId);
        if (photo.ThumbnailStoredName is not null) await TryDeleteAsync(photo.ThumbnailStoredName, claimId, photoId);
        return ClaimVersion.Format(claim.RowVersion);
    }

    private async Task TryDeleteAsync(string storedName, long claimId, long photoId)
    {
        try
        {
            await storage.DeleteAsync(storedName, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Claim {ClaimId} photo {PhotoId} was deleted but its stored file {StoredName} could not be removed", claimId, photoId, storedName);
        }
    }
}
