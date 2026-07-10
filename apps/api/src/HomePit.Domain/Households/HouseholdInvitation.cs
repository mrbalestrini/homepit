using HomePit.Domain.Common;

namespace HomePit.Domain.Households;

public sealed class HouseholdInvitation : AuditableEntity
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid InviterUserId { get; set; }
    public AppUser? InviterUser { get; set; }

    public string InviteeEmail { get; set; } = string.Empty;
    public HouseholdRole Role { get; set; } = HouseholdRole.Member;
    public HouseholdInvitationStatus Status { get; set; } = HouseholdInvitationStatus.Pending;
    public DateTimeOffset InvitedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
}
