using Claim.Domain.Claims;

namespace Claim.Application.Claims;

/// <summary>Deleting a Claim Photo (ADR 0006). The Claim's status never changes here.</summary>
public sealed class ClaimPhotoRemovalService(
    IClaimRepository claims,
    IClaimPhotoRepository photos,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    TimeProvider clock)
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

        var entry = claim.RecordPhotoRemoved(photo.OriginalFileName, actorUserId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.RecordPhotoRemovedAsync(photo, claim, entry, ct);
        await storage.DeleteAsync(photo.StoredName, ct);
        return ClaimVersion.Format(claim.RowVersion);
    }
}
