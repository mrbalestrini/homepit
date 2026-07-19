using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Application.Auth;

public interface ITokenService
{
    DateTimeOffset AccessTokenExpiresAt { get; }
    string CreateAccessToken(AppUser user, IReadOnlyCollection<SpaceMember> memberships);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
