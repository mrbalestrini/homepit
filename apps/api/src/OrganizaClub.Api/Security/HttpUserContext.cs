using System.Security.Claims;
using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Integrations;

namespace OrganizaClub.Api.Security;

public sealed class HttpUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
        }
    }

    public SystemRole SystemRole
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue("system_role");
            return Enum.TryParse<SystemRole>(value, ignoreCase: true, out var systemRole)
                ? systemRole
                : SystemRole.User;
        }
    }

    public Guid? SpaceId
    {
        get
        {
            var integrationValue = httpContextAccessor.HttpContext?.User.FindFirstValue("integration_space_id");
            if (Guid.TryParse(integrationValue, out var integrationSpaceId))
            {
                return integrationSpaceId;
            }

            var value = httpContextAccessor.HttpContext?.Request.Headers["X-Space-Id"].FirstOrDefault();
            return Guid.TryParse(value, out var spaceId) ? spaceId : null;
        }
    }

    public bool IsIntegration =>
        bool.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue("integration"), out var value) && value;

    public Guid? IntegrationConnectionId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue("integration_connection_id");
            return Guid.TryParse(value, out var connectionId) ? connectionId : null;
        }
    }

    public IntegrationAccessMode? IntegrationAccessMode
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue("integration_access_mode");
            return Enum.TryParse<IntegrationAccessMode>(value, ignoreCase: true, out var accessMode)
                ? accessMode
                : null;
        }
    }
}
