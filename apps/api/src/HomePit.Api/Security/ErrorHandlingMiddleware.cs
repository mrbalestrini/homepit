using HomePit.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HomePit.Api.Security;

public static class ErrorHandlingMiddleware
{
    public static IApplicationBuilder UseHomePitErrors(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                var (status, title) = exception switch
                {
                    UnauthorizedException => (StatusCodes.Status401Unauthorized, "Sessão inválida"),
                    ValidationException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
                    LockedException => (StatusCodes.Status423Locked, "Conta desativada"),
                    ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso negado"),
                    NotFoundException => (StatusCodes.Status404NotFound, "Não encontrado"),
                    ConflictException => (StatusCodes.Status409Conflict, "Conflito"),
                    _ => (StatusCodes.Status500InternalServerError, "Erro inesperado")
                };

                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("HomePit.Api.Errors");

                if (exception is AppException)
                {
                    logger.LogWarning(
                        exception,
                        "Request failed with handled application error. Status: {StatusCode}. Method: {Method}. Path: {Path}. TraceId: {TraceId}",
                        status,
                        context.Request.Method,
                        context.Request.Path,
                        context.TraceIdentifier);
                }
                else
                {
                    logger.LogError(
                        exception,
                        "Request failed with unexpected error. Status: {StatusCode}. Method: {Method}. Path: {Path}. TraceId: {TraceId}",
                        status,
                        context.Request.Method,
                        context.Request.Path,
                        context.TraceIdentifier);
                }

                var problem = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Detail = exception is AppException ? exception.Message : "Não foi possível concluir a operação."
                };

                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(problem);
            }
        });
    }
}
