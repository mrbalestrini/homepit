using HomePit.Application.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomePit.Infrastructure.Auth;

public sealed class AccountDeletionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AccountDeletionWorkerOptions> options,
    ILogger<AccountDeletionWorker> logger)
    : BackgroundService
{
    private readonly AccountDeletionWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Purge de contas desativado.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                var deleted = await authService.PurgeScheduledDeletionsAsync(stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Purge automático removeu {Count} conta(s).", deleted);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao executar purge automático de contas.");
            }

            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.PollIntervalMinutes)), stoppingToken);
        }
    }
}
