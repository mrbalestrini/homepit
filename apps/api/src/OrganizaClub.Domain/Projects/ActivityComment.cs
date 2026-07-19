using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Projects;

public sealed class ActivityComment : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }

    public Guid ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public Guid AuthorMemberId { get; set; }
    public SpaceMember? AuthorMember { get; set; }

    public required string Body { get; set; }
}
