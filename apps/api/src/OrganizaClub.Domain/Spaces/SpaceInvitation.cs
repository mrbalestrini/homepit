using OrganizaClub.Domain.Common;

namespace OrganizaClub.Domain.Spaces;

public sealed class SpaceInvitation : AuditableEntity
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid InviterUserId { get; set; }
    public AppUser? InviterUser { get; set; }

    public string InviteeEmail { get; set; } = string.Empty;
    public SpaceRole Role { get; set; } = SpaceRole.Member;
    public SpaceInvitationStatus Status { get; set; } = SpaceInvitationStatus.Pending;
    public DateTimeOffset InvitedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
}
