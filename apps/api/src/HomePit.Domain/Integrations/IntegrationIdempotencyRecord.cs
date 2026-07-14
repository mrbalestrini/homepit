using HomePit.Domain.Common;

namespace HomePit.Domain.Integrations;

public sealed class IntegrationIdempotencyRecord : AuditableEntity
{
    public Guid IntegrationConnectionId { get; set; }
    public IntegrationConnection? IntegrationConnection { get; set; }
    public required string Operation { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string RequestHash { get; set; }
    public required string ResponseJson { get; set; }
    public int StatusCode { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
