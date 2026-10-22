using Claim.Domain.Claims;
using ClaimEntity = Claim.Domain.Claims.Claim;

namespace Claim.Application.Claims;

/// <summary>A Claim and one of its photos, as the role asking is allowed to see them.</summary>
public sealed record FoundPhoto(ClaimEntity Claim, ClaimPhoto Photo);

/// <summary>
/// The one way to reach a single Claim Photo: the Claim must exist and be visible to the role, and the photo must belong to
/// that Claim. A photo id of another Claim is "not found", never "forbidden", so ids cannot be probed across Claims.
/// </summary>
public sealed class ClaimPhotoLookup(IClaimRepository claims, IClaimPhotoRepository photos)
{
    /// <summary>Null when the Claim does not exist, the role may not see it, or it has no such photo.</summary>
    public async Task<FoundPhoto?> FindAsync(long claimId, long photoId, string role, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(claimId, ct);
        if (claim is null || !claim.IsVisibleTo(role)) return null;
        var photo = (await photos.ListAsync(claimId, ct)).FirstOrDefault(p => p.Id == photoId);
        return photo is null ? null : new FoundPhoto(claim, photo);
    }
}
