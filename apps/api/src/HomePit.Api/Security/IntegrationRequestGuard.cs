using HomePit.Application.Common;
using HomePit.Domain.Integrations;

namespace HomePit.Api.Security;

public static class IntegrationRequestGuard
{
    public static ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Request.Headers.ContainsKey("X-Household-Id"))
        {
            throw new ValidationException("A conexão já está vinculada a uma casa e não aceita X-Household-Id.");
        }

        var isWrite = !HttpMethods.IsGet(context.HttpContext.Request.Method) && !HttpMethods.IsHead(context.HttpContext.Request.Method);
        var accessMode = context.HttpContext.User.FindFirst("integration_access_mode")?.Value;
        if (isWrite && string.Equals(accessMode, IntegrationAccessMode.ReadOnly.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Esta conexão permite somente leitura.");
        }

        return next(context);
    }
}
