using HomePit.Domain.Integrations;
using System.Text.Json.Serialization;

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
    [property: JsonPropertyName("client_name")]
    string ClientName,
    [property: JsonPropertyName("redirect_uris")]
    IReadOnlyCollection<string> RedirectUris,
    [property: JsonPropertyName("grant_types")]
    IReadOnlyCollection<string>? GrantTypes,
    [property: JsonPropertyName("response_types")]
    IReadOnlyCollection<string>? ResponseTypes,
    [property: JsonPropertyName("token_endpoint_auth_method")]
    string? TokenEndpointAuthMethod);
