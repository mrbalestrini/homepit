using HomePit.Domain.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;

namespace HomePit.Domain.Prompts;

public sealed class Prompt : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public Guid? UniverseId { get; set; }
    public Universe? Universe { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string PromptText { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkTitle { get; set; }
    public bool IsArchived { get; set; }
    public string? ImageObjectKey { get; set; }
    public string? ImageContentType { get; set; }
    public DateTimeOffset? ImageUpdatedAt { get; set; }

    public ICollection<PromptCategoryAssignment> CategoryAssignments { get; } = new List<PromptCategoryAssignment>();
}
