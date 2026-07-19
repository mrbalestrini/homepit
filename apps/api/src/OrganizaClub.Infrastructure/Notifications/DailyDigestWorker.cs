using OrganizaClub.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrganizaClub.Infrastructure.Notifications;

public sealed class DailyDigestWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DailyDigestWorkerOptions> options,
    ILogger<DailyDigestWorker> logger)
    : BackgroundService
{
    private readonly DailyDigestWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DailyDigestEnabled)
        {
            logger.LogInformation("Resumo diário por WhatsApp desativado.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var digestService = scope.ServiceProvider.GetRequiredService<DailyDigestService>();
                var sent = await digestService.SendDueDigestsAsync(stoppingToken);
                if (sent > 0)
                {
                    logger.LogInformation("Resumo diário enviado para {Count} membro(s).", sent);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao processar resumo diário por WhatsApp.");
            }

            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.PollIntervalMinutes)), stoppingToken);
        }
    }
}
