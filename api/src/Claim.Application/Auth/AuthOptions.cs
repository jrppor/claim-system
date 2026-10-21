namespace Claim.Application.Auth;

public sealed class AuthOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "claim-api";
    public string Audience { get; set; } = "claim-ui";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
