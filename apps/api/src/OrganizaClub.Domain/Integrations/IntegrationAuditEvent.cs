using OrganizaClub.Domain.Common;

namespace OrganizaClub.Domain.Integrations;

public sealed class IntegrationAuditEvent : AuditableEntity
{
    public Guid IntegrationConnectionId { get; set; }
    public IntegrationConnection? IntegrationConnection { get; set; }
    public required string Surface { get; set; }
    public required string Operation { get; set; }
    public string? ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public int StatusCode { get; set; }
    public required string TraceId { get; set; }
}
