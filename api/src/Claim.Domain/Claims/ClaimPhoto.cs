namespace Claim.Domain.Claims;

/// <summary>
/// A Claim Photo: damage evidence attached to one Claim by Garage Staff. Only metadata lives here; the bytes are kept by
/// IFileStorage under <see cref="StoredName"/> (a GUID, never the original name) - ADR 0006.
/// </summary>
public sealed record ClaimPhoto(
    long ClaimId,
    string OriginalFileName,
    string StoredName,
    string ContentType,
    long SizeBytes,
    string? Caption,
    long UploadedByUserId,
    DateTime UploadedAt)
{
    public long Id { get; init; }

    public ClaimPhoto Stored(long id) => this with { Id = id };
}

/// <summary>The kinds of picture a Claim Photo may be, recognised from the first bytes of the file, never from its name or declared type.</summary>
public static class PhotoFormat
{
    public const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>Enough leading bytes to tell every supported format apart.</summary>
    public const int HeaderLength = 12;

    /// <summary>The content type for a JPEG, PNG or WebP header; null when the bytes are anything else.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return "image/jpeg";
        if (header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    public const int MaxFileNameLength = 255;

    /// <summary>
    /// The name to show people: only the last path segment, without control characters, at most 255 characters (the extension is kept).
    /// It is display text only; the stored file is named by a GUID.
    /// </summary>
    public static string DisplayName(string? fileName)
    {
        var name = fileName ?? "";
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0) return "photo";
        if (name.Length <= MaxFileNameLength) return name;
        var extension = Path.GetExtension(name);
        if (extension.Length > 10) extension = "";
        return name[..(MaxFileNameLength - extension.Length)] + extension;
    }

    /// <summary>The extension the stored file gets, derived from the detected type.</summary>
    public static string Extension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        _ => ".webp",
    };
}
