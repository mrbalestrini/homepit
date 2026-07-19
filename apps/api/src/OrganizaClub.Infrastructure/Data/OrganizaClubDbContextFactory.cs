using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenIddict.EntityFrameworkCore;

namespace OrganizaClub.Infrastructure.Data;

public sealed class OrganizaClubDbContextFactory : IDesignTimeDbContextFactory<OrganizaClubDbContext>
{
    public OrganizaClubDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OrganizaClubDb")
            ?? "Host=localhost;Port=5432;Database=organiza_club;Username=organiza_club;Password=organiza_club";
        var options = new DbContextOptionsBuilder<OrganizaClubDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "organiza_club"))
            .UseOpenIddict()
            .Options;
        return new OrganizaClubDbContext(options);
    }
}
