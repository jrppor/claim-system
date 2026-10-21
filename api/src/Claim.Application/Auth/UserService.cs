namespace Claim.Application.Auth;

public sealed class UserService(IUserRepository users)
{
    public async Task<IReadOnlyList<UserInfo>> ListAsync(CancellationToken ct) =>
        (await users.ListAsync(ct)).Select(AuthService.ToInfo).ToList();
}
