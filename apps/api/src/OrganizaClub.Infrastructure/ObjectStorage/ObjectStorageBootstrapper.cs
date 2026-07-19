using OrganizaClub.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OrganizaClub.Infrastructure.ObjectStorage;

public static class ObjectStorageBootstrapper
{
    public static async Task EnsureOrganizaClubObjectStorageAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var options = scope.ServiceProvider.GetRequiredService<ObjectStorageOptions>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("OrganizaClub.ObjectStorageBootstrapper");

        const int maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(3);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await storage.EnsureBucketExistsAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Falha ao inicializar o bucket do storage em {Endpoint}. Nova tentativa {Attempt}/{MaxAttempts} em {DelaySeconds}s.",
                    options.Endpoint,
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        await storage.EnsureBucketExistsAsync(cancellationToken);
    }
}
