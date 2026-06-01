using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HomePit.Application.Auth;
using HomePit.Domain.Households;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HomePit.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public DateTimeOffset AccessTokenExpiresAt => timeProvider.GetUtcNow().AddMinutes(_options.AccessTokenMinutes);

    public string CreateAccessToken(AppUser user, IReadOnlyCollection<HouseholdMember> memberships)
    {
        var now = timeProvider.GetUtcNow();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new("system_role", user.SystemRole.ToString())
        };

        claims.AddRange(memberships.Select(member => new Claim("household", member.HouseholdId.ToString())));
        claims.AddRange(memberships.Select(member => new Claim($"household:{member.HouseholdId}:role", member.Role.ToString())));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            now.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }
}
