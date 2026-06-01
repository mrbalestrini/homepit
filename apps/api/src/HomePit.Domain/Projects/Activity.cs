using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Projects;

public sealed class Activity : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? ResponsibleMemberId { get; set; }
    public HouseholdMember? ResponsibleMember { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public ActivityStatus Status { get; set; } = ActivityStatus.NaoIniciada;
    public Priority Priority { get; set; } = Priority.Media;
    public decimal? Size { get; set; }

    public ICollection<PendingItem> PendingItems { get; } = new List<PendingItem>();
    public ICollection<ActivityComment> Comments { get; } = new List<ActivityComment>();
}
