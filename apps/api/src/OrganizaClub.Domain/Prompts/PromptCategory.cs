using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Prompts;

public sealed class PromptCategory : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public required string Name { get; set; }

    public ICollection<PromptCategoryAssignment> PromptAssignments { get; } = new List<PromptCategoryAssignment>();
}
