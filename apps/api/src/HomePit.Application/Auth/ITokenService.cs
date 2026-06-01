using HomePit.Domain.Households;

namespace HomePit.Application.Auth;

public interface ITokenService
{
    DateTimeOffset AccessTokenExpiresAt { get; }
    string CreateAccessToken(AppUser user, IReadOnlyCollection<HouseholdMember> memberships);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
