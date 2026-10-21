using Claim.Domain.Claims;

namespace Claim.Application.Claims;

public interface IClaimRepository
{
    /// <summary>Next value of the SQL Sequence behind Claim numbers (CLM-yyyy-000001).</summary>
    Task<long> NextClaimSequenceAsync(CancellationToken ct);
    Task<long> AddAsync(Domain.Claims.Claim claim, CancellationToken ct);
    Task<Domain.Claims.Claim?> FindByIdAsync(long id, CancellationToken ct);
    Task UpdateAsync(Domain.Claims.Claim claim, CancellationToken ct);
}

public interface IPolicyRepository
{
    /// <summary>Policies whose number or plate contains the text (case-insensitive).</summary>
    Task<IReadOnlyList<Policy>> SearchAsync(string text, CancellationToken ct);
    Task<Policy?> FindByIdAsync(long id, CancellationToken ct);
}
