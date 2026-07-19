using OrganizaClub.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Api.Security;

public static class IntegrationConcurrencyFilter
{
    public static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PreconditionFailedException("O recurso foi alterado desde a última leitura.");
        }
    }
}
