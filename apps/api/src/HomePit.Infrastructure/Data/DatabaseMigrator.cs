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
}
