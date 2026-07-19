using OrganizaClub.Application.Auth;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Images;
using OrganizaClub.Application.Integrations;
using OrganizaClub.Application.Notifications;
using OrganizaClub.Application.Storage;
using OrganizaClub.Infrastructure.Auth;
using OrganizaClub.Infrastructure.Data;
using OrganizaClub.Infrastructure.Images;
using OrganizaClub.Infrastructure.Notifications;
using OrganizaClub.Infrastructure.ObjectStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using OpenIddict.EntityFrameworkCore;

namespace OrganizaClub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrganizaClubInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrganizaClubDbContext>(options =>
            options.UseNpgsql(
                    configuration.GetConnectionString("OrganizaClubDb"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "organiza_club"))
                .UseOpenIddict());

        services.AddScoped<IOrganizaClubDbContext>(provider => provider.GetRequiredService<OrganizaClubDbContext>());
        services.AddSingleton(TimeProvider.System);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<IntegrationOptions>(configuration.GetSection(IntegrationOptions.SectionName));
        services.Configure<SuperAdminOptions>(configuration.GetSection(SuperAdminOptions.SectionName));
        services.Configure<AccountDeletionWorkerOptions>(configuration.GetSection(AccountDeletionWorkerOptions.SectionName));
        services.Configure<EvolutionOptions>(configuration.GetSection(EvolutionOptions.SectionName));
        services.Configure<DailyDigestWorkerOptions>(configuration.GetSection(DailyDigestWorkerOptions.SectionName));
        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.SectionName));
        services.AddSingleton(provider => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SuperAdminOptions>>().Value);
        services.AddSingleton(provider => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ObjectStorageOptions>>().Value);

        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IImageUploadProcessor, ImageSharpImageUploadProcessor>();
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
        services.AddHostedService<AccountDeletionWorker>();
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
