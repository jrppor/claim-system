using Claim.Application.Claims;
using SkiaSharp;

namespace Claim.Infrastructure.Claims;

/// <summary>
/// Thumbnails with SkiaSharp (MIT). The longest side becomes <see cref="IThumbnailGenerator.MaxSide"/> pixels; smaller images are
/// kept as they are. The picture is turned upright from its EXIF orientation, because the thumbnail is re-encoded without EXIF
/// and phone photos would otherwise appear sideways.
/// </summary>
public sealed class SkiaThumbnailGenerator : IThumbnailGenerator
{
    public async Task<byte[]?> CreateAsync(Stream image, string contentType, CancellationToken ct)
    {
        // Skia takes over (and disposes) the stream it is given, so hand it a copy and leave the caller's stream alone.
        using var copy = new MemoryStream();
        await image.CopyToAsync(copy, ct);
        copy.Position = 0;
        return Create(copy, contentType);
    }

    private static byte[]? Create(Stream image, string contentType)
    {
        using var codec = SKCodec.Create(image);
        if (codec is null) return null;
        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null) return null;

        using var upright = Orient(decoded, codec.EncodedOrigin);
        var scale = Math.Min(1.0, (double)IThumbnailGenerator.MaxSide / Math.Max(upright.Width, upright.Height));
        using var sized = scale < 1.0 ? Shrink(upright, scale) : null;
        using var picture = SKImage.FromBitmap(sized ?? upright);

        var format = contentType switch
        {
            "image/png" => SKEncodedImageFormat.Png,
            "image/webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Jpeg,
        };
        using var encoded = picture.Encode(format, 80);
        return encoded?.ToArray();
    }

    private static SKBitmap? Shrink(SKBitmap source, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }

    /// <summary>A copy of the bitmap turned/mirrored so it displays the way the camera was held; the same bitmap when no change is needed.</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width, h = source.Height;
        SKMatrix? matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => null,
        };
        if (matrix is null) return source.Copy();

        var swapsSides = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(swapsSides ? source.Height : source.Width, swapsSides ? source.Width : source.Height);
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(matrix.Value);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }
}
