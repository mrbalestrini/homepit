using System.Security.Claims;
using HomePit.Application.Common;

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

    public Guid? HouseholdId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers["X-Household-Id"].FirstOrDefault();
            return Guid.TryParse(value, out var householdId) ? householdId : null;
        }
    }
}
