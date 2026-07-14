using HomePit.Domain.Integrations;

namespace HomePit.Application.Integrations;

public sealed record CreateIntegrationConnectionRequest(
    string Name,
    Guid HouseholdId,
    IntegrationAccessMode AccessMode,
    DateTimeOffset ExpiresAt);

public sealed record IntegrationConnectionDto(
    Guid Id,
    string Name,
    IntegrationCredentialKind CredentialKind,
    IntegrationAccessMode AccessMode,
    Guid HouseholdId,
    string HouseholdName,
    string? TokenPrefix,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    bool IsActive);

public sealed record CreatedIntegrationConnectionDto(
    IntegrationConnectionDto Connection,
    string Token,
    string RestApiUrl,
    string McpUrl);

public sealed record IntegrationSpaceDto(
    Guid Id,
    string Name,
    string HouseholdRole,
    IntegrationAccessMode AccessMode,
    DateTimeOffset ExpiresAt,
    string Timezone,
    string ContractVersion);

public sealed record IntegrationAuthenticatedPrincipal(
    Guid ConnectionId,
    Guid UserId,
    Guid HouseholdId,
    string DisplayName,
    string SystemRole,
    string HouseholdRole,
    IntegrationAccessMode AccessMode);
