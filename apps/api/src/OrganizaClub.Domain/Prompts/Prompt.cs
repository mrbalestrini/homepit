using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Projects;

namespace OrganizaClub.Domain.Prompts;

public sealed class Prompt : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public Guid? CoreId { get; set; }
    public Core? Core { get; set; }

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
