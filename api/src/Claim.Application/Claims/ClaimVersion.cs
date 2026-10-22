namespace Claim.Application.Claims;

/// <summary>Thrown when a Claim was changed by someone else after the caller read it (HTTP 409). Every stale-version path uses this one message.</summary>
public sealed class ConcurrencyException(string claimNumber)
    : Exception($"Claim {claimNumber} was changed by someone else after it was read.");

/// <summary>Thrown when a client sends a version that is not a version token (HTTP 400).</summary>
public sealed class InvalidVersionException() : Exception("The Claim version is not valid; send the `version` of the Claim JSON as If-Match.");

/// <summary>Thrown when a request that changes a Claim does not say which version it read (HTTP 428).</summary>
public sealed class VersionRequiredException() : Exception("Send the `version` of the Claim JSON you read as the If-Match header.");

/// <summary>The wire form of a Claim's rowversion: base64 text in the `version` field, echoed back by clients as If-Match.</summary>
public static class ClaimVersion
{
    public static string Format(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static byte[] Parse(string text)
    {
        try { return Convert.FromBase64String(text); }
        catch (FormatException) { throw new InvalidVersionException(); }
    }

    /// <summary>
    /// The single stale-version check for every Claim-changing operation, including a Repair Estimate that leaves the Claim's
    /// status alone (and so writes no Claims row for the Unit of Work to compare). Writes later compare the same version again
    /// in SQL, which covers a change that sneaks in between this check and the write.
    /// </summary>
    public static void EnsureVersion(this Domain.Claims.Claim claim, byte[] expected)
    {
        if (!claim.RowVersion.AsSpan().SequenceEqual(expected)) throw new ConcurrencyException(claim.ClaimNumber);
    }
}
