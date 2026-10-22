using System.Net.Mail;
using Claim.Domain.Claims;
using ClaimEntity = Claim.Domain.Claims.Claim;

namespace Claim.Application.Claims;

/// <summary>Input for creating or editing a Draft Claim; only the Policy is mandatory at this stage.</summary>
public sealed record ClaimInput(
    long? PolicyId,
    string? VehiclePlate,
    string? VehicleMake,
    string? VehicleModel,
    string? CustomerEmail,
    string? DamageDescription);

public sealed record ClaimDto(
    long Id,
    string ClaimNumber,
    string Status,
    Policy Policy,
    string? VehiclePlate,
    string? VehicleMake,
    string? VehicleModel,
    string? CustomerEmail,
    string? DamageDescription,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal? ApprovedAmount = null,
    long? ApprovedEstimateId = null,
    string? Version = null);

/// <summary>Limits shared by every free-text field a Claim carries (reason, comments, note, damage description).</summary>
public static class ClaimLimits
{
    public const int MaxTextLength = 2000;
}

public sealed class ClaimValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("Claim input is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class ClaimService(IClaimRepository claims, IPolicyRepository policies, TimeProvider clock)
{
    public async Task<ClaimDto> CreateDraftAsync(ClaimInput input, long userId, CancellationToken ct)
    {
        var errors = ValidateDetails(input);
        Policy? policy = null;
        if (input.PolicyId is null) errors["policyId"] = ["Policy is required."];
        else
        {
            policy = await policies.FindByIdAsync(input.PolicyId.Value, ct);
            if (policy is null) errors["policyId"] = ["Policy not found."];
        }
        if (errors.Count > 0) throw new ClaimValidationException(errors);

        var now = clock.GetUtcNow().UtcDateTime;
        var number = ClaimEntity.FormatNumber(now.Year, await claims.NextClaimSequenceAsync(ct));
        var draft = ClaimEntity.CreateDraft(number, policy!.Id, ToDetails(input), userId, now);
        var id = await claims.AddAsync(draft, ct);
        return ToDto((await claims.FindByIdAsync(id, ct))!, policy);
    }

    /// <summary>Returns null when the Claim does not exist or the role may not see it (a Draft is invisible to the Adjuster).</summary>
    public async Task<ClaimDto?> GetAsync(long id, string role, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(id, ct);
        return claim is null || !claim.IsVisibleTo(role) ? null : await ToDtoAsync(claim, ct);
    }

    /// <summary>Edits a Draft. The Policy a Claim was opened under never changes. Returns null when the Claim does not exist.</summary>
    public async Task<ClaimDto?> EditDraftAsync(long id, ClaimInput input, byte[] expectedVersion, CancellationToken ct)
    {
        var errors = ValidateDetails(input);
        if (errors.Count > 0) throw new ClaimValidationException(errors);

        var claim = await claims.FindByIdAsync(id, ct);
        if (claim is null) return null;

        claim.EnsureVersion(expectedVersion);
        claim.EditDraft(ToDetails(input), clock.GetUtcNow().UtcDateTime);
        await claims.UpdateAsync(claim, ct);
        return await ToDtoAsync((await claims.FindByIdAsync(id, ct))!, ct);
    }

    private async Task<ClaimDto> ToDtoAsync(ClaimEntity c, CancellationToken ct) =>
        ToDto(c, (await policies.FindByIdAsync(c.PolicyId, ct))!);

    private static Dictionary<string, string[]> ValidateDetails(ClaimInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var email = Clean(input.CustomerEmail);
        if (email is not null && !IsEmail(email)) errors["customerEmail"] = ["Customer email is not a valid email address."];
        if (Clean(input.DamageDescription)?.Length > ClaimLimits.MaxTextLength) errors["damageDescription"] = [$"Damage description must be at most {ClaimLimits.MaxTextLength} characters."];
        return errors;
    }

    private static bool IsEmail(string s) =>
        s.Length <= 256 && MailAddress.TryCreate(s, out var a) && a.Address == s && s.Contains('.', StringComparison.Ordinal);

    private static ClaimDetails ToDetails(ClaimInput i) => new(
        Clean(i.VehiclePlate), Clean(i.VehicleMake), Clean(i.VehicleModel), Clean(i.CustomerEmail), Clean(i.DamageDescription));

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ClaimDto ToDto(ClaimEntity c, Policy policy) => new(
        c.Id, c.ClaimNumber, c.Status.ToString(), policy,
        c.Details.VehiclePlate, c.Details.VehicleMake, c.Details.VehicleModel,
        c.Details.CustomerEmail, c.Details.DamageDescription, c.CreatedAt, c.UpdatedAt, c.ApprovedAmount, c.ApprovedEstimateId,
        ClaimVersion.Format(c.RowVersion));
}
