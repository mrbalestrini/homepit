using System.Security.Claims;
using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Integrations;
using OrganizaClub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Api.Security;

public sealed class OAuthMcpRequirement : IAuthorizationRequirement;

public sealed class OAuthMcpAuthorizationHandler(OrganizaClubDbContext db, TimeProvider timeProvider)
    : AuthorizationHandler<OAuthMcpRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OAuthMcpRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(OpenIddict.Abstractions.OpenIddictConstants.Claims.Subject);
        var connectionId = context.User.FindFirstValue("integration_connection_id");
        var spaceId = context.User.FindFirstValue("integration_space_id");
        var scope = context.User.FindFirstValue(OpenIddict.Abstractions.OpenIddictConstants.Claims.Scope) ?? string.Empty;
        if (!Guid.TryParse(userId, out var parsedUserId) || !Guid.TryParse(connectionId, out var parsedConnectionId) ||
            !Guid.TryParse(spaceId, out var parsedSpaceId) || !scope.Split(' ').Contains("organiza.read", StringComparer.Ordinal))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var connection = await db.IntegrationConnections
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == parsedConnectionId && item.UserId == parsedUserId && item.SpaceId == parsedSpaceId);
        if (connection?.User is null || connection.CredentialKind != IntegrationCredentialKind.OAuthGrant || connection.IsRevoked ||
            connection.ExpiresAt <= now || !connection.User.IsActive || connection.User.SystemRole == SystemRole.SuperAdmin)
        {
            return;
        }

        var membershipIsActive = await db.SpaceMembers.AnyAsync(item =>
            item.UserId == parsedUserId && item.SpaceId == parsedSpaceId && item.IsActive);
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
