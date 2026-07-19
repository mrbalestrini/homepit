using System.Linq;
using System.Reflection;
using OrganizaClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class MigrationMetadataTests
{
    [Fact]
    public void All_migrations_are_discoverable_by_ef_core()
    {
        var migrations = typeof(OrganizaClubDbContext).Assembly
            .GetTypes()
            .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        Assert.NotEmpty(migrations);

        foreach (var migration in migrations)
        {
            var dbContextAttribute = migration.GetCustomAttribute<DbContextAttribute>();
            var migrationAttribute = migration.GetCustomAttribute<MigrationAttribute>();

            Assert.NotNull(dbContextAttribute);
            Assert.Equal(typeof(OrganizaClubDbContext), dbContextAttribute!.ContextType);
            Assert.NotNull(migrationAttribute);
            Assert.False(string.IsNullOrWhiteSpace(migrationAttribute!.Id));
        }
    }
}
