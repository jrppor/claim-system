using Claim.Domain.Claims;
using Microsoft.Extensions.Logging;

namespace Claim.Application.Claims;

/// <summary>Deleting a Claim Photo (ADR 0006). The Claim's status never changes here.</summary>
public sealed class ClaimPhotoRemovalService(
    IClaimRepository claims,
    IClaimPhotoRepository photos,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    TimeProvider clock,
    ILogger<ClaimPhotoRemovalService> logger)
{
    /// <summary>
    /// Removes the row and its Claim History line in one transaction, and only then the file: a failure before the commit leaves
    /// photo and file both intact; a failure of the file delete after the commit leaves only an unreferenced file, never a row
    /// pointing at a missing file. Returns the Claim's new version, or null when the Claim, photo or role's view does not exist.
    /// </summary>
    public async Task<string?> RemoveAsync(
        long claimId, long photoId, long actorUserId, string actorRole, byte[] expectedVersion, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(claimId, ct);
        if (claim is null || !claim.IsVisibleTo(actorRole)) return null;
        var photo = (await photos.ListAsync(claimId, ct)).FirstOrDefault(p => p.Id == photoId);
        if (photo is null) return null;
        claim.EnsureVersion(expectedVersion);

        var entry = claim.RecordPhotoRemoved(photo.OriginalFileName, actorUserId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.RecordPhotoRemovedAsync(photo, claim, entry, ct);
        try
        {
            await storage.DeleteAsync(photo.StoredName, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The deletion is already committed; answering with an error would invite a retry that can only 404. The leftover
            // file is unreferenced and harmless, but must be findable for cleanup.
            logger.LogWarning(ex, "Claim {ClaimId} photo {PhotoId} was deleted but its stored file {StoredName} could not be removed", claimId, photoId, photo.StoredName);
        }
        return ClaimVersion.Format(claim.RowVersion);
    }
}
