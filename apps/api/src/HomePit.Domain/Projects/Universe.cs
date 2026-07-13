using HomePit.Domain.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Prompts;

namespace HomePit.Domain.Projects;

public sealed class Universe : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public required string Name { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageObjectKey { get; set; }
    public string? ImageContentType { get; set; }
    public DateTimeOffset? ImageUpdatedAt { get; set; }
    public ICollection<Project> Projects { get; } = new List<Project>();
    public ICollection<MemberEffortAllocation> EffortAllocations { get; } = new List<MemberEffortAllocation>();
    public ICollection<Prompt> Prompts { get; } = new List<Prompt>();
}
