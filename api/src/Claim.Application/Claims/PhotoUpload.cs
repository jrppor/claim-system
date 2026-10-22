namespace Claim.Application.Claims;

/// <summary>One file of an upload request as the web layer received it; the service opens it, reads it once and disposes the stream.</summary>
public sealed record IncomingPhoto(string FileName, long Length, Func<Stream> Open);

/// <summary>One file being checked and stored, with the caption that pairs with it; the service owns <see cref="Content"/>.</summary>
internal sealed record PhotoUpload(string FileName, long Length, Stream Content, string? Caption);

/// <summary>Which stored file of a Claim Photo to read.</summary>
public enum PhotoVariant
{
    Original,
    Thumbnail,
}
