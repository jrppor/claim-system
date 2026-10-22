using Claim.Application.Claims;

namespace Claim.Api.Claims;

/// <summary>
/// Reads the version a client read from the `If-Match` header (the Claim JSON's `version`, quoted or not).
/// Absent or `*` means the client did not say, so no stale-version check is made against it.
/// </summary>
public static class IfMatch
{
    public static byte[]? ExpectedVersion(this HttpRequest request)
    {
        var header = request.Headers.IfMatch.ToString().Trim();
        if (header.Length == 0 || header == "*") return null;
        return ClaimVersion.Parse(header.Trim('"'));
    }
}
