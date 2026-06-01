using HomePit.Application.Common;
using Microsoft.AspNetCore.Mvc;

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
                    ValidationException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
                    ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso negado"),
                    NotFoundException => (StatusCodes.Status404NotFound, "Não encontrado"),
                    ConflictException => (StatusCodes.Status409Conflict, "Conflito"),
                    _ => (StatusCodes.Status500InternalServerError, "Erro inesperado")
                };

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
