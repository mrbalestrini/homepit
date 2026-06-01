namespace HomePit.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "homepit";
    public string Audience { get; set; } = "homepit";
    public string SigningKey { get; set; } = "change-this-development-key-with-at-least-32-chars";
    public int AccessTokenMinutes { get; set; } = 30;
}
