using System.Security.Claims;
using HomePit.Application.Common;
using HomePit.Domain.Households;

namespace HomePit.Api.Security;

public sealed class HttpUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
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

    public Guid? HouseholdId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers["X-Household-Id"].FirstOrDefault();
            return Guid.TryParse(value, out var householdId) ? householdId : null;
        }
    }
}
