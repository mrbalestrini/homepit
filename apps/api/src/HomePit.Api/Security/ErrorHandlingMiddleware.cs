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
                var (status, title, code, retryable) = exception switch
                {
                    UnauthorizedException => (StatusCodes.Status401Unauthorized, "Sessão inválida", "unauthorized", false),
                    ValidationException => (StatusCodes.Status400BadRequest, "Requisição inválida", "validation_error", false),
                    LockedException => (StatusCodes.Status423Locked, "Conta desativada", "account_locked", false),
                    ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso negado", "forbidden", false),
                    NotFoundException => (StatusCodes.Status404NotFound, "Não encontrado", "not_found", false),
                    ConflictException => (StatusCodes.Status409Conflict, "Conflito", "conflict", false),
                    PreconditionRequiredException => (StatusCodes.Status428PreconditionRequired, "Pré-condição obrigatória", "precondition_required", false),
                    PreconditionFailedException => (StatusCodes.Status412PreconditionFailed, "Pré-condição não atendida", "precondition_failed", false),
                    _ => (StatusCodes.Status500InternalServerError, "Erro inesperado", "internal_error", true)
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
                problem.Extensions["code"] = code;
                problem.Extensions["traceId"] = context.TraceIdentifier;
                problem.Extensions["retryable"] = retryable;

                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(problem);
            }
        });
    }
}
