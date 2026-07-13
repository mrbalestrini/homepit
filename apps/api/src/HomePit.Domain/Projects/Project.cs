using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Projects;

public sealed class Project : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }

    public Guid UniverseId { get; set; }
    public Universe? Universe { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public required string Name { get; set; }
    public ICollection<Activity> Activities { get; } = new List<Activity>();
    public ICollection<MemberEffortAllocation> EffortAllocations { get; } = new List<MemberEffortAllocation>();
}
