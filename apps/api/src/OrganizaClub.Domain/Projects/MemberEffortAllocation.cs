using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Projects;

public sealed class MemberEffortAllocation : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid SpaceMemberId { get; set; }
    public SpaceMember? SpaceMember { get; set; }

    public Guid? CoreId { get; set; }
    public Core? Core { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public EffortScopeType ScopeType { get; set; }
    public EffortWeekday Weekday { get; set; }
    public decimal Points { get; set; }
}
