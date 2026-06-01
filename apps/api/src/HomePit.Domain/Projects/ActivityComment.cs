using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Projects;

public sealed class ActivityComment : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }

    public Guid ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public Guid AuthorMemberId { get; set; }
    public HouseholdMember? AuthorMember { get; set; }

    public required string Body { get; set; }
}
