using System.Security.Claims;

namespace Claim.Api.Http;

/// <summary>Who is calling: the `sub` and `role` claims of the access token, read in one place.</summary>
public sealed record Actor(long UserId, string Role)
{
    public static Actor From(ClaimsPrincipal user) =>
        new(long.Parse(user.FindFirst("sub")!.Value), user.FindFirst("role")!.Value);
}
