using HomePit.Domain.Common;

namespace HomePit.Domain.Households;

public sealed class RefreshToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsRevoked => RevokedAt.HasValue;
}
