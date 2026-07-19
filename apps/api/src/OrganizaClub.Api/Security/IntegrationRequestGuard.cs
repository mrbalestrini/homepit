using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Integrations;

namespace OrganizaClub.Api.Security;

public static class IntegrationRequestGuard
{
    public static ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Request.Headers.ContainsKey("X-Space-Id"))
        {
            throw new ValidationException("A conexão já está vinculada a um espaço e não aceita X-Space-Id.");
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
