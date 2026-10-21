using System.Security.Claims;
using System.Text;
using Claim.Application.Auth;
using Claim.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Claim.Infrastructure.Auth;

public sealed class JwtAccessTokenIssuer(IOptions<AuthOptions> options) : IAccessTokenIssuer
{
    public AccessToken Issue(User user)
    {
        var o = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey));
        var lifetime = TimeSpan.FromMinutes(o.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            Expires = DateTime.UtcNow.Add(lifetime),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new System.Security.Claims.Claim("sub", user.Id.ToString()),
                new System.Security.Claims.Claim("email", user.Email),
                new System.Security.Claims.Claim("name", user.DisplayName),
                new System.Security.Claims.Claim("role", user.Role),
            ]),
        };
        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), (int)lifetime.TotalSeconds);
    }
}
