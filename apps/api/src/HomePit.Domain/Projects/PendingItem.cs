using HomePit.Domain.Common;

namespace HomePit.Domain.Projects;

public sealed class PendingItem : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }

    public Guid ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public Priority Priority { get; set; } = Priority.Media;
    public DateOnly? DueDate { get; set; }
    public int? SnoozeDays { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public bool IsCompleted => CompletedAt.HasValue;
}
