using OrganizaClub.Domain.Integrations;

namespace OrganizaClub.Application.Integrations;

public sealed record CreateIntegrationConnectionRequest(
    string Name,
    Guid SpaceId,
    IntegrationAccessMode AccessMode,
    DateTimeOffset ExpiresAt);

public sealed record IntegrationConnectionDto(
    Guid Id,
    string Name,
    IntegrationCredentialKind CredentialKind,
    IntegrationAccessMode AccessMode,
    Guid SpaceId,
    string SpaceName,
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
    string SpaceRole,
    IntegrationAccessMode AccessMode,
    DateTimeOffset ExpiresAt,
    string Timezone,
    string ContractVersion);

public sealed record IntegrationAuthenticatedPrincipal(
    Guid ConnectionId,
    Guid UserId,
    Guid SpaceId,
    string DisplayName,
    string SystemRole,
    string SpaceRole,
    IntegrationAccessMode AccessMode);
