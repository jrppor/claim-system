namespace Claim.Application.Claims;

/// <summary>One file of an upload request with its optional caption; the caller keeps ownership of <see cref="Content"/>.</summary>
public sealed record PhotoUpload(string FileName, long Length, Stream Content, string? Caption);
