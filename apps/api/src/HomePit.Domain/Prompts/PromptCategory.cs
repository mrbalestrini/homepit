using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Prompts;

public sealed class PromptCategory : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public required string Name { get; set; }

    public ICollection<PromptCategoryAssignment> PromptAssignments { get; } = new List<PromptCategoryAssignment>();
}
