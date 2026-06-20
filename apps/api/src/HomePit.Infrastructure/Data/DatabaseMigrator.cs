using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HomePit.Infrastructure.Data;

public static class DatabaseMigrator
{
    public static async Task MigrateHomePitDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("HomePit.Infrastructure.Migrations");

        if (!db.Database.IsRelational())
        {
            logger.LogInformation("Skipping HomePit migration application because the provider is not relational.");
            return;
        }

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var pending = pendingMigrations.ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("HomePit database is up to date. No pending migrations were found.");
            return;
        }

        logger.LogInformation(
            "Applying {MigrationCount} pending HomePit migration(s): {MigrationNames}",
            pending.Length,
            string.Join(", ", pending));

        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("HomePit database migrations applied successfully.");
    }

    public static async Task EnsureNoPendingHomePitMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("HomePit.Infrastructure.Migrations");

        if (!db.Database.IsRelational())
        {
            logger.LogInformation("Skipping HomePit pending migration check because the provider is not relational.");
            return;
        }

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var pending = pendingMigrations.ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("HomePit database is up to date. No pending migrations were found.");
            return;
        }

        logger.LogError(
            "HomePit database has {MigrationCount} pending migration(s): {MigrationNames}",
            pending.Length,
            string.Join(", ", pending));

        throw new InvalidOperationException(
            $"Existem migrations pendentes no banco HomePit: {string.Join(", ", pending)}. " +
            "Aplique as migrations antes de iniciar a API ou habilite Database:ApplyMigrationsOnStartup.");
    }
}
