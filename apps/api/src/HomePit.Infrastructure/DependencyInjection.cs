using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Notifications;
using HomePit.Infrastructure.Auth;
using HomePit.Infrastructure.Data;
using HomePit.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomePit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHomePitInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HomePitDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("HomePitDb")));

        services.AddScoped<IHomePitDbContext>(provider => provider.GetRequiredService<HomePitDbContext>());
        services.AddSingleton(TimeProvider.System);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<EvolutionOptions>(configuration.GetSection(EvolutionOptions.SectionName));
        services.Configure<DailyDigestWorkerOptions>(configuration.GetSection(DailyDigestWorkerOptions.SectionName));

        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<DailyDigestService>();
        services.AddHttpClient<IWhatsAppClient, EvolutionApiWhatsAppClient>();
        services.AddHostedService<DailyDigestWorker>();

        return services;
    }
}
