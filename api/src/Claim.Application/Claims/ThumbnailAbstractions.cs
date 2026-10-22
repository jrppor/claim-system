namespace Claim.Application.Claims;

/// <summary>
/// Makes the one thumbnail size of a Claim Photo. Kept apart from IFileStorage and the upload rules so the image library
/// is one replaceable adapter.
/// </summary>
public interface IThumbnailGenerator
{
    /// <summary>Longest side of a thumbnail, in pixels. Smaller images are never enlarged.</summary>
    public const int MaxSide = 320;

    /// <summary>
    /// The encoded thumbnail (same format as <paramref name="contentType"/>), or null when the bytes cannot be decoded as an image.
    /// </summary>
    Task<byte[]?> CreateAsync(Stream image, string contentType, CancellationToken ct);
}
