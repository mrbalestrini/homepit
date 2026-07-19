using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Integrations;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Integrations;

public sealed class IntegrationIdempotencyService(
    IOrganizaClubDbContext db,
    IUserContext userContext,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> ExecuteAsync<T>(
        string operation,
        string? idempotencyKey,
        object request,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (!userContext.IsIntegration || userContext.IntegrationConnectionId is not Guid connectionId)
        {
            return await action();
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 128)
        {
            throw new ValidationException("Informe uma chave de idempotência de até 128 caracteres.");
        }

        var normalizedKey = idempotencyKey.Trim();
        var requestHash = Hash(JsonSerializer.Serialize(request, SerializerOptions));
        var existing = await db.IntegrationIdempotencyRecords
            .FirstOrDefaultAsync(item =>
                item.IntegrationConnectionId == connectionId &&
                item.Operation == operation &&
                item.IdempotencyKey == normalizedKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.ExpiresAt <= timeProvider.GetUtcNow())
            {
                db.IntegrationIdempotencyRecords.Remove(existing);
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new ConflictException("A chave de idempotência já foi usada com outro conteúdo.");
            }
            else
            {
                return JsonSerializer.Deserialize<T>(existing.ResponseJson, SerializerOptions)
                    ?? throw new ConflictException("Não foi possível recuperar a resposta idempotente.");
            }
        }

        var response = await action();
        db.IntegrationIdempotencyRecords.Add(new IntegrationIdempotencyRecord
        {
            IntegrationConnectionId = connectionId,
            Operation = operation,
            IdempotencyKey = normalizedKey,
            RequestHash = requestHash,
            ResponseJson = JsonSerializer.Serialize(response, SerializerOptions),
            StatusCode = 201,
            ExpiresAt = timeProvider.GetUtcNow().AddDays(90)
        });
        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
