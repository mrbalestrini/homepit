namespace OrganizaClub.Application.Integrations;

public sealed class IntegrationOptions
{
    public const string SectionName = "Integrations";

    public bool Enabled { get; init; } = true;
    public string TokenPepper { get; init; } = string.Empty;
    public int RequestsPerMinute { get; init; } = 60;
    public int AuditRetentionDays { get; init; } = 90;
    public string PublicBaseUrl { get; init; } = "http://localhost:8080";
}
