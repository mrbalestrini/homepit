using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Projects;

public sealed class Project : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }

    public Guid CoreId { get; set; }
    public Core? Core { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public required string Name { get; set; }
    public ICollection<Activity> Activities { get; } = new List<Activity>();
    public ICollection<MemberEffortAllocation> EffortAllocations { get; } = new List<MemberEffortAllocation>();
}
