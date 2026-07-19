using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Notifications;

public sealed class NotificationPreference : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid SpaceMemberId { get; set; }
    public SpaceMember? SpaceMember { get; set; }

    public bool DailyDigestEnabled { get; set; } = true;
    public string? WhatsAppPhoneNumber { get; set; }
    public TimeOnly DailyDigestTime { get; set; } = new(8, 0);
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
}
