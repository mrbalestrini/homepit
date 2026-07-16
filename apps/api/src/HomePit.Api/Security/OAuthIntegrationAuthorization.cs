using System.Security.Claims;
using HomePit.Application.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Integrations;
using HomePit.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Api.Security;

public sealed class OAuthMcpRequirement : IAuthorizationRequirement;

public sealed class OAuthMcpAuthorizationHandler(HomePitDbContext db, TimeProvider timeProvider)
    : AuthorizationHandler<OAuthMcpRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OAuthMcpRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(OpenIddict.Abstractions.OpenIddictConstants.Claims.Subject);
        var connectionId = context.User.FindFirstValue("integration_connection_id");
        var householdId = context.User.FindFirstValue("integration_household_id");
        var scope = context.User.FindFirstValue(OpenIddict.Abstractions.OpenIddictConstants.Claims.Scope) ?? string.Empty;
        if (!Guid.TryParse(userId, out var parsedUserId) || !Guid.TryParse(connectionId, out var parsedConnectionId) ||
            !Guid.TryParse(householdId, out var parsedHouseholdId) || !scope.Split(' ').Contains("homepit.read", StringComparer.Ordinal))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var connection = await db.IntegrationConnections
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == parsedConnectionId && item.UserId == parsedUserId && item.HouseholdId == parsedHouseholdId);
        if (connection?.User is null || connection.CredentialKind != IntegrationCredentialKind.OAuthGrant || connection.IsRevoked ||
            connection.ExpiresAt <= now || !connection.User.IsActive || connection.User.SystemRole == SystemRole.SuperAdmin)
        {
            return;
        }

        var membershipIsActive = await db.HouseholdMembers.AnyAsync(item =>
            item.UserId == parsedUserId && item.HouseholdId == parsedHouseholdId && item.IsActive);
        if (!membershipIsActive)
        {
            return;
        }

        if (!connection.LastUsedAt.HasValue || connection.LastUsedAt.Value.AddMinutes(5) <= now)
        {
            connection.LastUsedAt = now;
            await db.SaveChangesAsync();
        }

        context.Succeed(requirement);
    }
}
