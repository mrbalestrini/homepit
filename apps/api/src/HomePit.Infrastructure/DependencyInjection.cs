using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Notifications;
using HomePit.Application.Storage;
using HomePit.Infrastructure.Auth;
using HomePit.Infrastructure.Data;
using HomePit.Infrastructure.Notifications;
using HomePit.Infrastructure.ObjectStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

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
        services.Configure<SuperAdminOptions>(configuration.GetSection(SuperAdminOptions.SectionName));
        services.Configure<EvolutionOptions>(configuration.GetSection(EvolutionOptions.SectionName));
        services.Configure<DailyDigestWorkerOptions>(configuration.GetSection(DailyDigestWorkerOptions.SectionName));
        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.SectionName));
        services.AddSingleton(provider => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SuperAdminOptions>>().Value);
        services.AddSingleton(provider => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ObjectStorageOptions>>().Value);

        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<DailyDigestService>();
        services.AddHttpClient<IWhatsAppClient, EvolutionApiWhatsAppClient>();
        services.AddSingleton<IMinioClient>(provider =>
        {
            var options = provider.GetRequiredService<ObjectStorageOptions>();
            var (endpoint, port, useSsl) = NormalizeEndpoint(options);

            var builder = new MinioClient()
                .WithEndpoint(endpoint, port)
                .WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(useSsl);

            return builder.Build();
        });
        services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        services.AddHostedService<DailyDigestWorker>();

        return services;
    }

    private static (string Endpoint, int Port, bool UseSsl) NormalizeEndpoint(ObjectStorageOptions options)
    {
        var value = options.Endpoint.Trim().TrimEnd('/');
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return (uri.Host, uri.IsDefaultPort ? (options.UseSsl || uri.Scheme == Uri.UriSchemeHttps ? 443 : 80) : uri.Port, options.UseSsl || uri.Scheme == Uri.UriSchemeHttps);
        }

        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[1], out var port)
            ? (parts[0], port, options.UseSsl)
            : (value, options.UseSsl ? 443 : 80, options.UseSsl);
    }
}
