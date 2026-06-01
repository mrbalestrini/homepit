using HomePit.Domain.Common;

namespace HomePit.Domain.Households;

public sealed class AppUser : AuditableEntity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ProfilePhotoObjectKey { get; set; }
    public DateTimeOffset? ProfilePhotoUpdatedAt { get; set; }
    public SystemRole SystemRole { get; set; } = SystemRole.User;
    public bool IsActive { get; set; } = true;

    public ICollection<HouseholdMember> HouseholdMembers { get; } = new List<HouseholdMember>();
    public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();
}
