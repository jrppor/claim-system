using Claim.Domain.Claims;

namespace Claim.Application.Claims;

public sealed class PolicyService(IPolicyRepository policies)
{
    public Task<IReadOnlyList<Policy>> SearchAsync(string? text, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(text)
            ? Task.FromResult<IReadOnlyList<Policy>>([])
            : policies.SearchAsync(text.Trim(), ct);
}
