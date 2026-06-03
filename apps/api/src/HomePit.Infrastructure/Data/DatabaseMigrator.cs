using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomePit.Infrastructure.Data;

public static class DatabaseMigrator
{
    public static async Task MigrateHomePitDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public static async Task EnsureNoPendingHomePitMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var pending = pendingMigrations.ToArray();

        if (pending.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Existem migrations pendentes no banco HomePit: {string.Join(", ", pending)}. " +
            "Aplique as migrations antes de iniciar a API ou habilite Database:ApplyMigrationsOnStartup.");
    }
}
