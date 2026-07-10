using HomePit.Domain.Households;
using HomePit.Domain.Plans;

namespace HomePit.Application.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string? PhoneNumber);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UpdateProfileRequest(string DisplayName, string? PhoneNumber);

public sealed record DeleteOwnAccountResult(
    bool DeletedImmediately,
    DateTimeOffset? ScheduledDeletionAt);

public sealed record AdminUserListItemDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? PhoneNumber,
    SystemRole SystemRole,
    AccountState AccountState,
    DateTimeOffset? ScheduledDeletionAt,
    DateTimeOffset? DeactivatedAt,
    int OwnedHouseholdCount,
    int MembershipCount,
    bool IsProtected,
    string EffectivePlanSlug,
    string EffectivePlanName,
    DateTimeOffset? ActiveSubscriptionStartsAt,
    DateTimeOffset? ActiveSubscriptionEndsAt,
    BillingCycle? ActiveSubscriptionBillingCycle,
    decimal? ActiveSubscriptionAmountPaid,
    string? ActiveSubscriptionCurrencyCode,
    UserSubscriptionStatus? ActiveSubscriptionStatus);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserDto User,
    IReadOnlyCollection<HouseholdDto> Households);

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? PhoneNumber,
    SystemRole SystemRole,
    AccountState AccountState,
    DateTimeOffset? ScheduledDeletionAt,
    string? SupportEmail,
    bool HasProfilePhoto,
    DateTimeOffset? ProfilePhotoUpdatedAt);

public sealed record HouseholdDto(
    Guid Id,
    string Name,
    HouseholdRole Role,
    DateTimeOffset CreatedAt,
    bool IsOwnedByCurrentUser);
