using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Projects;

public sealed class MemberEffortAllocation : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }

    public Guid? UniverseId { get; set; }
    public Universe? Universe { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public EffortScopeType ScopeType { get; set; }
    public EffortWeekday Weekday { get; set; }
    public decimal Points { get; set; }
}
