using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Notifications;

public sealed class NotificationPreference : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid HouseholdMemberId { get; set; }
    public HouseholdMember? HouseholdMember { get; set; }

    public bool DailyDigestEnabled { get; set; } = true;
    public string? WhatsAppPhoneNumber { get; set; }
    public TimeOnly DailyDigestTime { get; set; } = new(8, 0);
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
}
