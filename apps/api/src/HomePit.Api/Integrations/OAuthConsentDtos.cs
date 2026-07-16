using HomePit.Domain.Integrations;

namespace HomePit.Api.Integrations;

public sealed record OAuthConsentInteractionDto(
    string ClientName,
    IReadOnlyCollection<string> RequestedScopes,
    DateTimeOffset ExpiresAt);

public sealed record ApproveOAuthConsentRequest(
    Guid HouseholdId,
    IntegrationAccessMode AccessMode,
    DateTimeOffset ExpiresAt);

public sealed record OAuthConsentContinuationDto(string ContinueUrl);

public sealed record DynamicClientRegistrationRequest(
    string ClientName,
    IReadOnlyCollection<string> RedirectUris,
    IReadOnlyCollection<string>? GrantTypes,
    IReadOnlyCollection<string>? ResponseTypes,
    string? TokenEndpointAuthMethod);
