using OrganizaClub.Application.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OrganizaClub.Infrastructure.Data;

public static class DatabaseMigrator
{
    public static async Task MigrateOrganizaClubDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizaClubDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("OrganizaClub.Infrastructure.Migrations");

        if (!db.Database.IsRelational())
        {
            logger.LogInformation("Skipping OrganizaClub migration application because the provider is not relational.");
            return;
        }

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var pending = pendingMigrations.ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("OrganizaClub database is up to date. No pending migrations were found.");
            return;
        }

        logger.LogInformation(
            "Applying {MigrationCount} pending OrganizaClub migration(s): {MigrationNames}",
            pending.Length,
            string.Join(", ", pending));

        await db.Database.MigrateAsync(cancellationToken);

        var commercialPlanService = scope.ServiceProvider.GetRequiredService<CommercialPlanService>();
        await commercialPlanService.EnsurePlanCatalogAsync(cancellationToken);

        logger.LogInformation("OrganizaClub database migrations applied successfully.");
    }

    public static async Task EnsureNoPendingOrganizaClubMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizaClubDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("OrganizaClub.Infrastructure.Migrations");

        if (!db.Database.IsRelational())
        {
            logger.LogInformation("Skipping OrganizaClub pending migration check because the provider is not relational.");
            return;
        }

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var pending = pendingMigrations.ToArray();

        if (pending.Length == 0)
        {
            var commercialPlanService = scope.ServiceProvider.GetRequiredService<CommercialPlanService>();
            await commercialPlanService.EnsurePlanCatalogAsync(cancellationToken);
            logger.LogInformation("OrganizaClub database is up to date. No pending migrations were found.");
            return;
        }

        logger.LogError(
            "OrganizaClub database has {MigrationCount} pending migration(s): {MigrationNames}",
            pending.Length,
            string.Join(", ", pending));

        throw new InvalidOperationException(
            $"Existem migrations pendentes no banco OrganizaClub: {string.Join(", ", pending)}. " +
            "Aplique as migrations antes de iniciar a API ou habilite Database:ApplyMigrationsOnStartup.");
    }
}
