using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Integrations;

public sealed class IntegrationConnection : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public required string Name { get; set; }
    public IntegrationCredentialKind CredentialKind { get; set; } = IntegrationCredentialKind.ApiKey;
    public IntegrationAccessMode AccessMode { get; set; } = IntegrationAccessMode.ReadOnly;
    public string? KeyId { get; set; }
    public string? SecretHash { get; set; }
    public string? TokenPrefix { get; set; }
    public string? OAuthAuthorizationId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public bool IsRevoked => RevokedAt.HasValue;
}
