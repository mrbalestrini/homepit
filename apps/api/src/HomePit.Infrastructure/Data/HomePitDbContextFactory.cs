using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenIddict.EntityFrameworkCore;

namespace HomePit.Infrastructure.Data;

public sealed class HomePitDbContextFactory : IDesignTimeDbContextFactory<HomePitDbContext>
{
    public HomePitDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__HomePitDb")
            ?? "Host=localhost;Port=5432;Database=homepit;Username=homepit;Password=homepit";
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseNpgsql(connectionString)
            .UseOpenIddict()
            .Options;
        return new HomePitDbContext(options);
    }
}
