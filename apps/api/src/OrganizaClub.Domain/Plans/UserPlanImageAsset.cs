using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Plans;

public sealed class UserPlanImageAsset : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public PlanImageAssetModule Module { get; set; }
    public Guid EntityId { get; set; }
    public required string ObjectKey { get; set; }
    public required string ContentType { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public bool IsDegraded { get; set; }
    public DateTimeOffset? DegradedAt { get; set; }
}
