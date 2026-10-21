namespace Claim.Domain.Users;

public sealed record User(
    long Id,
    string Email,
    string PasswordHash,
    string DisplayName,
    string Role,
    bool IsActive);
