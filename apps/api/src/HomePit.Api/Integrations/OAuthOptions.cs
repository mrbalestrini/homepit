namespace HomePit.Api.Integrations;

public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";

    public string Issuer { get; init; } = "http://localhost:8080";
    public string WebConsentUrl { get; init; } = "http://localhost:3000/oauth/consent";
    public string SigningKey { get; init; } = string.Empty;
    public string EncryptionKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
    public int InteractionMinutes { get; init; } = 10;
    public int DynamicRegistrationRequestsPerMinute { get; init; } = 10;
    public string[] TrustedProxies { get; init; } = [];

    public string CanonicalMcpResource => $"{Issuer.TrimEnd('/')}/mcp";
}
