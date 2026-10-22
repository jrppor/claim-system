using Claim.Domain.Claims;

namespace Claim.Application.Claims;

/// <summary>
/// Where the bytes of Claim Photos live (ADR 0006). Names are opaque keys chosen by the application (GUIDs), never user input.
/// The disk implementation is one adapter; a cloud blob store can replace it without touching Application.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string name, Stream content, CancellationToken ct);

    /// <summary>The stored bytes, or null when no file has that name. The caller disposes the stream.</summary>
    Task<Stream?> OpenReadAsync(string name, CancellationToken ct);

    /// <summary>Removes the file; a missing file is not an error.</summary>
    Task DeleteAsync(string name, CancellationToken ct);
}

public interface IClaimPhotoRepository
{
    /// <summary>Photos of one Claim, oldest first.</summary>
    Task<IReadOnlyList<ClaimPhoto>> ListAsync(long claimId, CancellationToken ct);
}

public sealed record ClaimPhotoDto(
    long Id, long ClaimId, string OriginalFileName, string ContentType, long SizeBytes, string? Caption,
    long UploadedByUserId, DateTime UploadedAt, string Version);

/// <summary>A photo's bytes with the type that was verified when it was uploaded.</summary>
public sealed record PhotoFile(Stream Content, string ContentType);
