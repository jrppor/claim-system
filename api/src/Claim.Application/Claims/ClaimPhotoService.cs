using Claim.Domain.Claims;
using Microsoft.Extensions.Logging;
using ClaimEntity = Claim.Domain.Claims.Claim;

namespace Claim.Application.Claims;

/// <summary>Claim Photos: attaching and reading damage evidence (ADR 0006). The Claim's status never changes here.</summary>
public sealed class ClaimPhotoService(
    IClaimRepository claims,
    IClaimPhotoRepository photos,
    ClaimPhotoLookup lookup,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    IThumbnailGenerator thumbnails,
    TimeProvider clock,
    ILogger<ClaimPhotoService> logger)
{
    /// <summary>
    /// Pairs each file with its caption by position (a missing caption means none), checks every file, stores originals and
    /// thumbnails, then the rows and their Claim History lines in one transaction. If anything fails the files stored so far
    /// are removed again (including one whose write itself failed half-way), so neither an orphan file nor a half-saved row is
    /// left. One bad file rejects the whole request. Returns null when the Claim does not exist or the role may not see it.
    /// </summary>
    public async Task<IReadOnlyList<ClaimPhotoDto>?> UploadAsync(
        long claimId, IReadOnlyList<IncomingPhoto> files, IReadOnlyList<string?> captions,
        long actorUserId, string actorRole, byte[] expectedVersion, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(claimId, ct);
        if (claim is null || !claim.IsVisibleTo(actorRole)) return null;
        claim.EnsureVersion(expectedVersion);

        claim.EnsureAcceptsPhotoChange();
        if (files.Count == 0) throw new ClaimValidationException(new Dictionary<string, string[]> { ["file"] = ["Choose at least one image file."] });
        // The count below is read outside the transaction, so two racing uploads can both see room. That is safe because every
        // upload sends the Claim's version and RecordPhotosAsync rewrites the Claim row only if that version is still current
        // (rolling the photo rows back otherwise): of two uploads holding the same version one commits, the other gets a 409.
        // Refuse an oversized batch before the database or any file content is read; the exact count is checked right after.
        claim.EnsureRoomForPhotos(0, files.Count);
        claim.EnsureRoomForPhotos((await photos.ListAsync(claimId, ct)).Count, files.Count);

        var uploads = new List<PhotoUpload>();
        try
        {
            for (var i = 0; i < files.Count; i++)
                uploads.Add(new PhotoUpload(files[i].FileName, files[i].Length, files[i].Open(), i < captions.Count ? captions[i] : null));
            return await StoreAsync(claim, uploads, actorUserId, ct);
        }
        finally
        {
            foreach (var upload in uploads) await upload.Content.DisposeAsync();
        }
    }

    private async Task<IReadOnlyList<ClaimPhotoDto>> StoreAsync(ClaimEntity claim, List<PhotoUpload> uploads, long actorUserId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var drafts = new List<ClaimPhoto>();
        var entries = new List<ClaimHistoryEntry>();
        foreach (var upload in uploads)
        {
            var fileName = PhotoFormat.DisplayName(upload.FileName);
            if (upload.Length > PhotoFormat.MaxBytes)
                throw ClaimValidationException.ForFile(fileName, $"the file is larger than {PhotoFormat.MaxBytes / (1024 * 1024)} MB.");

            var caption = PhotoFormat.NormalizeCaption(upload.Caption);
            if (caption is { Length: > PhotoFormat.MaxCaptionLength })
                throw ClaimValidationException.ForFile(fileName, $"the caption is longer than {PhotoFormat.MaxCaptionLength} characters.", "caption");

            var header = new byte[PhotoFormat.HeaderLength];
            var read = await upload.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
            var contentType = PhotoFormat.Detect(header.AsSpan(0, read));
            if (contentType is null)
                throw ClaimValidationException.ForFile(fileName, "only JPEG, PNG and WebP images are accepted.");
            upload.Content.Position = 0;

            var storedName = Guid.NewGuid().ToString("N") + PhotoFormat.Extension(contentType);
            drafts.Add(new ClaimPhoto(claim.Id, fileName, storedName, contentType, upload.Length, caption, actorUserId, now));
            entries.Add(claim.RecordPhotoAdded(fileName, actorUserId, now));
        }

        var attempted = new List<string>();
        try
        {
            for (var i = 0; i < drafts.Count; i++)
            {
                var thumbnail = await CreateThumbnailAsync(uploads[i].Content, drafts[i], ct);
                var thumbnailName = Guid.NewGuid().ToString("N") + "_thumb" + PhotoFormat.Extension(drafts[i].ContentType);
                // A name counts as stored from the moment its write starts: a write that throws half-way can leave a partial file.
                attempted.Add(thumbnailName);
                await storage.SaveAsync(thumbnailName, new MemoryStream(thumbnail), ct);

                drafts[i] = drafts[i] with { ThumbnailStoredName = thumbnailName };
                uploads[i].Content.Position = 0;
                attempted.Add(drafts[i].StoredName);
                await storage.SaveAsync(drafts[i].StoredName, uploads[i].Content, ct);
            }
            var stored = await unitOfWork.RecordPhotosAsync(drafts, claim, entries, ct);
            return stored.Select(p => ToDto(p, claim)).ToList();
        }
        catch
        {
            await RemoveQuietlyAsync(attempted);
            throw;
        }
    }

    /// <summary>The thumbnail bytes of a verified image; an unreadable or oversized picture rejects the whole request, naming the file.</summary>
    private async Task<byte[]> CreateThumbnailAsync(Stream image, ClaimPhoto draft, CancellationToken ct)
    {
        var result = await thumbnails.CreateAsync(image, draft.ContentType, ct);
        return result switch
        {
            ThumbnailResult.Created created => created.Bytes,
            ThumbnailResult.TooLarge large => throw ClaimValidationException.ForFile(draft.OriginalFileName,
                $"the image is too large ({large.Width} x {large.Height} px); at most {PhotoFormat.MaxPixels / 1_000_000} megapixels and {PhotoFormat.MaxSide} px on a side are accepted."),
            _ => throw ClaimValidationException.ForFile(draft.OriginalFileName, "the file is not a readable image."),
        };
    }

    /// <summary>Best-effort undo of an upload that failed; a file that cannot be removed is logged by name and does not hide the original failure.</summary>
    private async Task RemoveQuietlyAsync(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            try
            {
                await storage.DeleteAsync(name, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stored file {StoredName} could not be removed while undoing a failed photo upload", name);
            }
        }
    }

    /// <summary>Photos of a Claim, oldest first. Null when the Claim does not exist or the role may not see it.</summary>
    public async Task<IReadOnlyList<ClaimPhotoDto>?> ListAsync(long claimId, string role, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(claimId, ct);
        if (claim is null || !claim.IsVisibleTo(role)) return null;
        return (await photos.ListAsync(claimId, ct)).Select(p => ToDto(p, claim)).ToList();
    }

    /// <summary>
    /// The stored bytes of one photo (original or thumbnail). Null when the Claim or photo does not exist, the role may not see
    /// them, or the photo has no such file. A row whose file is gone from storage is also null to the caller, but logged: it
    /// means data was lost, not that the client asked for something wrong.
    /// </summary>
    public async Task<PhotoFile?> OpenAsync(long claimId, long photoId, string role, PhotoVariant variant, CancellationToken ct)
    {
        var found = await lookup.FindAsync(claimId, photoId, role, ct);
        if (found is null) return null;
        var storedName = variant == PhotoVariant.Thumbnail ? found.Photo.ThumbnailStoredName : found.Photo.StoredName;
        if (storedName is null) return null;
        var stream = await storage.OpenReadAsync(storedName, ct);
        if (stream is null)
        {
            logger.LogWarning("Claim {ClaimId} photo {PhotoId} has a row but its stored file {StoredName} is missing", claimId, photoId, storedName);
            return null;
        }
        return new PhotoFile(stream, found.Photo.ContentType);
    }

    private static ClaimPhotoDto ToDto(ClaimPhoto p, ClaimEntity claim) => new(
        p.Id, p.ClaimId, p.OriginalFileName, p.ContentType, p.SizeBytes, p.Caption, p.UploadedByUserId, p.UploadedAt,
        ClaimVersion.Format(claim.RowVersion));
}
