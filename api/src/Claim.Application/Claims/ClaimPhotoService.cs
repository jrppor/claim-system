using Claim.Domain.Claims;
using ClaimEntity = Claim.Domain.Claims.Claim;

namespace Claim.Application.Claims;

/// <summary>Claim Photos: attaching and reading damage evidence (ADR 0006). The Claim's status never changes here.</summary>
public sealed class ClaimPhotoService(
    IClaimRepository claims,
    IClaimPhotoRepository photos,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    TimeProvider clock)
{
    /// <summary>
    /// Stores the files, then the rows and their Claim History lines in one transaction; if anything fails the files stored so far
    /// are removed again, so neither an orphan file nor a half-saved row is left. Every file is checked first and one bad file
    /// rejects the whole request. Returns null when the Claim does not exist or the role may not see it.
    /// </summary>
    public async Task<IReadOnlyList<ClaimPhotoDto>?> UploadAsync(
        long claimId, IReadOnlyList<PhotoUpload> uploads, long actorUserId, string actorRole, byte[] expectedVersion, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(claimId, ct);
        if (claim is null || !claim.IsVisibleTo(actorRole)) return null;
        claim.EnsureVersion(expectedVersion);

        claim.EnsureAcceptsPhotoChange();

        var now = clock.GetUtcNow().UtcDateTime;
        var drafts = new List<ClaimPhoto>();
        var entries = new List<ClaimHistoryEntry>();
        foreach (var upload in uploads)
        {
            var fileName = PhotoFormat.DisplayName(upload.FileName);
            if (upload.Length > PhotoFormat.MaxBytes)
                throw FileRejected($"{fileName}: the file is larger than {PhotoFormat.MaxBytes / (1024 * 1024)} MB.");

            var header = new byte[PhotoFormat.HeaderLength];
            var read = await upload.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
            var contentType = PhotoFormat.Detect(header.AsSpan(0, read));
            if (contentType is null)
                throw FileRejected($"{fileName}: only JPEG, PNG and WebP images are accepted.");
            upload.Content.Position = 0;

            var storedName = Guid.NewGuid().ToString("N") + PhotoFormat.Extension(contentType);
            drafts.Add(new ClaimPhoto(claimId, fileName, storedName, contentType, upload.Length, null, actorUserId, now));
            entries.Add(claim.RecordPhotoAdded(fileName, actorUserId, now));
        }

        var saved = new List<string>();
        try
        {
            for (var i = 0; i < drafts.Count; i++)
            {
                await storage.SaveAsync(drafts[i].StoredName, uploads[i].Content, ct);
                saved.Add(drafts[i].StoredName);
            }
            var stored = await unitOfWork.RecordPhotosAsync(drafts, claim, entries, ct);
            return stored.Select(p => ToDto(p, claim)).ToList();
        }
        catch
        {
            foreach (var name in saved) await storage.DeleteAsync(name, CancellationToken.None);
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
