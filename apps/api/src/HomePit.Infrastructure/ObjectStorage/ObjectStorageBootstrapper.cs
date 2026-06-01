using HomePit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace HomePit.Infrastructure.ObjectStorage;

public static class ObjectStorageBootstrapper
{
    public static async Task EnsureHomePitObjectStorageAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await storage.EnsureBucketExistsAsync(cancellationToken);
    }
}
