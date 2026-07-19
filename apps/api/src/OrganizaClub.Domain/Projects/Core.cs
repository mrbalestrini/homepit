using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Prompts;

namespace OrganizaClub.Domain.Projects;

public sealed class Core : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public required string Name { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageObjectKey { get; set; }
    public string? ImageContentType { get; set; }
    public DateTimeOffset? ImageUpdatedAt { get; set; }
    public ICollection<Project> Projects { get; } = new List<Project>();
    public ICollection<MemberEffortAllocation> EffortAllocations { get; } = new List<MemberEffortAllocation>();
    public ICollection<Prompt> Prompts { get; } = new List<Prompt>();
}
