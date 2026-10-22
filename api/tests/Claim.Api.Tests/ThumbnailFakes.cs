using Claim.Application.Claims;

namespace Claim.Api.Tests;

/// <summary>Stands in for the image library: the "thumbnail" is a marker plus the original size, so tests can tell it from the original.</summary>
public sealed class FakeThumbnailGenerator : IThumbnailGenerator
{
    public static byte[] ThumbnailOf(long originalLength) => System.Text.Encoding.ASCII.GetBytes($"THUMB:{originalLength}");

    private int _calls;

    /// <summary>The Nth <see cref="CreateAsync"/> call from now answers "not a decodable image" (1 = the next one).</summary>
    public int? FailOnCallNumber { get; set; }

    public async Task<ThumbnailResult> CreateAsync(Stream image, string contentType, CancellationToken ct)
    {
        if (FailOnCallNumber is { } n && ++_calls == n)
        {
            FailOnCallNumber = null;
            _calls = 0;
            return new ThumbnailResult.Unreadable();
        }
        using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, ct);
        return new ThumbnailResult.Created(ThumbnailOf(buffer.Length));
    }
}
