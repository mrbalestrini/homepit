using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Integrations;

public sealed class OAuthAuthorizationInteraction
{
    public Guid Id { get; set; }
    public required string TokenHash { get; set; }
    public required string ClientId { get; set; }
    public required string ClientName { get; set; }
    public required string RedirectUri { get; set; }
    public required string Scope { get; set; }
    public string? State { get; set; }
    public required string CodeChallenge { get; set; }
    public required string CodeChallengeMethod { get; set; }
    public required string Resource { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? DeniedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public AppUser? ApprovedByUser { get; set; }
    public Guid? IntegrationConnectionId { get; set; }
    public IntegrationConnection? IntegrationConnection { get; set; }
}
