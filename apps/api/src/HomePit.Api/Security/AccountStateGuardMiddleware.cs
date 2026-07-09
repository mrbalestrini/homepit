using HomePit.Application.Common;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Api.Security;

public static class AccountStateGuardMiddleware
{
    public static IApplicationBuilder UseAccountStateGuard(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api") ||
                context.User.Identity?.IsAuthenticated != true)
            {
                await next(context);
                return;
            }

            if (context.Request.Path.StartsWithSegments("/api/auth/refresh"))
            {
                await next(context);
                return;
            }

            var userContext = context.RequestServices.GetRequiredService<IUserContext>();
            if (userContext.UserId == Guid.Empty)
            {
                await next(context);
                return;
            }

            var db = context.RequestServices.GetRequiredService<HomePitDbContext>();
            var account = await db.Users
                .AsNoTracking()
                .Where(user => user.Id == userContext.UserId && user.IsActive)
                .Select(user => new
                {
                    user.AccountState,
                    user.ScheduledDeletionAt
                })
                .SingleOrDefaultAsync(context.RequestAborted);

            if (account is null)
            {
                throw new UnauthorizedException("Sessão expirada ou inválida.");
            }

            if (account.AccountState == Domain.Households.AccountState.Active)
            {
                await next(context);
                return;
            }

            if (context.Request.Path.StartsWithSegments("/api/users/me/reactivate") &&
                account.AccountState == Domain.Households.AccountState.PendingSelfDeletion)
            {
                await next(context);
                return;
            }

            if (account.AccountState == Domain.Households.AccountState.PendingSelfDeletion)
            {
                var scheduledDeletionAt = account.ScheduledDeletionAt?.ToLocalTime().ToString("dd/MM/yyyy 'às' HH:mm");
                throw new LockedException($"Sua conta está desativada e seus dados serão apagados em {scheduledDeletionAt}.");
            }

            var superAdminOptions = context.RequestServices.GetRequiredService<HomePit.Application.Auth.SuperAdminOptions>();
            throw new LockedException(
                $"Sua conta está desativada. Entre em contato com {superAdminOptions.SupportEmail ?? "o superadmin"}.");
        });
    }
}
