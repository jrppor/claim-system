namespace Claim.Application.Claims;

/// <summary>Thrown by the repositories / Unit of Work when a Claim was changed by someone else after the caller read it (HTTP 409).</summary>
public sealed class ConcurrencyException(string message) : Exception(message);

/// <summary>Thrown when a client sends a version that is not a version token (HTTP 400).</summary>
public sealed class InvalidVersionException() : Exception("The Claim version is not valid; send the `version` of the Claim JSON as If-Match.");

/// <summary>The wire form of a Claim's rowversion: base64 text in the `version` field, echoed back by clients as If-Match.</summary>
public static class ClaimVersion
{
    public static string Format(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static byte[] Parse(string text)
    {
        try { return Convert.FromBase64String(text); }
        catch (FormatException) { throw new InvalidVersionException(); }
    }
}
