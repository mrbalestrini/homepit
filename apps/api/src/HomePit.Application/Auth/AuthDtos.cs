using HomePit.Domain.Households;

namespace HomePit.Application.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string? HouseholdName,
    string? PhoneNumber);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UpdateProfileRequest(string DisplayName, string? PhoneNumber);

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
    bool HasProfilePhoto,
    DateTimeOffset? ProfilePhotoUpdatedAt);

public sealed record HouseholdDto(Guid Id, string Name, HouseholdRole Role);
