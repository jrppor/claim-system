using Claim.Application.Claims;

namespace Claim.Api.Claims;

/// <summary>
/// Reads the version a client read from the `If-Match` header (the Claim JSON's `version`, quoted or not).
/// The version is mandatory for every request that changes a Claim: absent → 428, and `*` ("any version") is not accepted,
/// so a client can never overwrite a Claim it has not seen.
/// </summary>
public static class IfMatch
{
    public static byte[] RequiredVersion(this HttpRequest request)
    {
        var header = request.Headers.IfMatch.ToString().Trim();
        if (header.Length == 0 || header == "*") throw new VersionRequiredException();
        return ClaimVersion.Parse(header.Trim('"'));
    }
}
