using System.Net.Http.Json;
using System.Text.Json;
using OrganizaClub.Application.Notifications;
using Microsoft.Extensions.Options;

namespace OrganizaClub.Infrastructure.Notifications;

public sealed class EvolutionApiWhatsAppClient(HttpClient httpClient, IOptions<EvolutionOptions> options)
    : IWhatsAppClient
{
    private readonly EvolutionOptions _options = options.Value;

    public async Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("EvolutionApi:ApiKey não foi configurado.");
        }

        httpClient.BaseAddress = new Uri(_options.BaseUrl);
        httpClient.DefaultRequestHeaders.Remove("apikey");
        httpClient.DefaultRequestHeaders.Add("apikey", _options.ApiKey);

        var path = _options.SendTextPathTemplate.Replace("{instance}", Uri.EscapeDataString(_options.InstanceName), StringComparison.Ordinal);
        var response = await httpClient.PostAsJsonAsync(path, new { number = phoneNumber, text = message }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var providerId = TryReadProviderId(body) ?? Guid.NewGuid().ToString("N");
        return new WhatsAppSendResult(providerId);
    }

    private static string? TryReadProviderId(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("key", out var key) &&
            key.TryGetProperty("id", out var id) &&
            id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }

        if (root.TryGetProperty("messageId", out var messageId) &&
            messageId.ValueKind == JsonValueKind.String)
        {
            return messageId.GetString();
        }

        return null;
    }
}
