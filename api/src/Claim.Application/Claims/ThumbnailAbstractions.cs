namespace Claim.Application.Claims;

/// <summary>What <see cref="IThumbnailGenerator.CreateAsync"/> made of an image.</summary>
public abstract record ThumbnailResult
{
    /// <summary>The encoded thumbnail.</summary>
    public sealed record Created(byte[] Bytes) : ThumbnailResult;

    /// <summary>The bytes are not an image the library can decode (corrupt, truncated, unsupported, or the decoder failed).</summary>
    public sealed record Unreadable : ThumbnailResult;

    /// <summary>The header declares more pixels than a photo may have; nothing was decoded.</summary>
    public sealed record TooLarge(int Width, int Height) : ThumbnailResult;
}

/// <summary>
/// Makes the one thumbnail size of a Claim Photo. Kept apart from IFileStorage and the upload rules so the image library
/// is one replaceable adapter.
/// </summary>
public interface IThumbnailGenerator
{
    /// <summary>Longest side of a thumbnail, in pixels. Smaller images are never enlarged.</summary>
    public const int MaxSide = 320;

    /// <summary>
    /// The thumbnail (same format as <paramref name="contentType"/>). The declared size is read from the image header first and an
    /// image above <see cref="Claim.Domain.Claims.PhotoFormat.MaxPixels"/> or <see cref="Claim.Domain.Claims.PhotoFormat.MaxSide"/>
    /// is refused before any pixel is decoded (a small file can declare a huge picture). Library failures are answered as
    /// <see cref="ThumbnailResult.Unreadable"/>, never thrown; only cancellation and the caller's own stream failing throw.
    /// </summary>
    Task<ThumbnailResult> CreateAsync(Stream image, string contentType, CancellationToken ct);
}
