using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Integrations;

namespace OrganizaClub.Api.Security;

/// <summary>Stores operational metadata only; request and response payloads are never audited.</summary>
public static class IntegrationAuditFilter
{
    public static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        var userContext = context.HttpContext.RequestServices.GetRequiredService<IUserContext>();
        if (userContext.IntegrationConnectionId is not Guid connectionId)
        {
            return result;
        }

        var segments = context.HttpContext.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var resourceType = segments.Length > 4 ? segments[4] : null;
        var db = context.HttpContext.RequestServices.GetRequiredService<IOrganizaClubDbContext>();
        db.IntegrationAuditEvents.Add(new IntegrationAuditEvent
        {
            IntegrationConnectionId = connectionId,
            Surface = "REST",
            Operation = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}",
            ResourceType = resourceType,
            StatusCode = context.HttpContext.Response.StatusCode is 0 ? StatusCodes.Status200OK : context.HttpContext.Response.StatusCode,
            TraceId = context.HttpContext.TraceIdentifier
        });
        await db.SaveChangesAsync(context.HttpContext.RequestAborted);
        return result;
    }
}
