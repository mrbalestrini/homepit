using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Notifications;

public sealed class NotificationRun : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }

    public required string Kind { get; set; }
    public DateOnly LocalDate { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public required string ProviderMessageId { get; set; }
}
