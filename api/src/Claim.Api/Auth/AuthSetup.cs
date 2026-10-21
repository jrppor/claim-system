using System.Text;
using Claim.Application.Auth;
using Claim.Domain.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Claim.Api.Auth;

/// <summary>Policy names double as role names so endpoints can say <c>.RequireAuthorization(AuthPolicies.Admin)</c>.</summary>
public static class AuthPolicies
{
    public const string GarageStaff = Role.GarageStaff;
    public const string InsuranceAdjuster = Role.InsuranceAdjuster;
    public const string Admin = Role.Admin;
}

public static class AuthSetup
{
    public const string CorsPolicy = "ui";

    public static IServiceCollection AddClaimAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
        if (jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 characters.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(5),
                };
            });

        services.AddAuthorization(o =>
        {
            foreach (var role in Role.All)
                o.AddPolicy(role, p => p.RequireAuthenticatedUser().RequireRole(role));
        });

        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
        services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }
}
