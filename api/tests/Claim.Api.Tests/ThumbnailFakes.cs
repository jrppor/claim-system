using Claim.Application.Claims;

namespace Claim.Api.Tests;

/// <summary>Stands in for the image library: the "thumbnail" is a marker plus the original size, so tests can tell it from the original.</summary>
public sealed class FakeThumbnailGenerator : IThumbnailGenerator
{
    public static byte[] ThumbnailOf(long originalLength) => System.Text.Encoding.ASCII.GetBytes($"THUMB:{originalLength}");

    public async Task<byte[]?> CreateAsync(Stream image, string contentType, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, ct);
        return ThumbnailOf(buffer.Length);
    }
}
