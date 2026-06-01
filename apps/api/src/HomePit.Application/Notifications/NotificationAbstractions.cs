namespace HomePit.Application.Notifications;

public interface IWhatsAppClient
{
    Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string message, CancellationToken cancellationToken);
}

public sealed record WhatsAppSendResult(string ProviderMessageId);
