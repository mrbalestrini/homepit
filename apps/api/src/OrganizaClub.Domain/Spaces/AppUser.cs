using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Plans;

namespace OrganizaClub.Domain.Spaces;

public sealed class AppUser : AuditableEntity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ProfilePhotoObjectKey { get; set; }
    public DateTimeOffset? ProfilePhotoUpdatedAt { get; set; }
    public SystemRole SystemRole { get; set; } = SystemRole.User;
    public AccountState AccountState { get; set; } = AccountState.Active;
    public DateTimeOffset? ScheduledDeletionAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
    public Guid? DeactivatedByUserId { get; set; }
    public AppUser? DeactivatedByUser { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<SpaceMember> SpaceMembers { get; } = new List<SpaceMember>();
    public ICollection<Space> CreatedSpaces { get; } = new List<Space>();
    public ICollection<SpaceInvitation> SentSpaceInvitations { get; } = new List<SpaceInvitation>();
    public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();
    public ICollection<AppUser> DeactivatedUsers { get; } = new List<AppUser>();
    public ICollection<UserSubscription> Subscriptions { get; } = new List<UserSubscription>();
    public ICollection<UserPlanImageAsset> PlanImageAssets { get; } = new List<UserPlanImageAsset>();
}
