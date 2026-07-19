using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Notifications;

public sealed class NotificationRun : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid SpaceMemberId { get; set; }
    public SpaceMember? SpaceMember { get; set; }

    public required string Kind { get; set; }
    public DateOnly LocalDate { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public required string ProviderMessageId { get; set; }
}
