using System.Security.Cryptography;
using System.Text;
using HomePit.Application.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HomePit.Application.Integrations;

public sealed class IntegrationConnectionService(
    IHomePitDbContext db,
    IUserContext userContext,
    TimeProvider timeProvider,
    IOptions<IntegrationOptions> options)
{
    private readonly IntegrationOptions options = options.Value;

    public async Task<IReadOnlyCollection<IntegrationConnectionDto>> ListCurrentUserAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await db.IntegrationConnections
            .AsNoTracking()
            .Include(connection => connection.Household)
            .Where(connection => connection.UserId == userContext.UserId)
            .OrderByDescending(connection => connection.CreatedAt)
            .Select(connection => new IntegrationConnectionDto(
                connection.Id,
                connection.Name,
                connection.CredentialKind,
                connection.AccessMode,
                connection.HouseholdId,
                connection.Household!.Name,
                connection.TokenPrefix,
                connection.ExpiresAt,
                connection.RevokedAt,
                connection.LastUsedAt,
                connection.CreatedAt,
                !connection.RevokedAt.HasValue && connection.ExpiresAt > now))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<CreatedIntegrationConnectionDto> CreateAsync(
        CreateIntegrationConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            throw new ForbiddenException("As conexões de integração não estão disponíveis no momento.");
        }

        EnsureTokenPepperConfigured();

        if (userContext.UserId == Guid.Empty || userContext.SystemRole == SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Esta conta não pode criar conexões de integração.");
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = request.ExpiresAt.ToUniversalTime();
        if (expiresAt <= now.AddDays(1) || expiresAt > now.AddDays(365))
        {
            throw new ValidationException("A expiração deve ficar entre amanhã e os próximos 365 dias.");
        }

        var name = RequiredText(request.Name, "Informe um nome para a conexão.");
        if (name.Length > 120)
        {
            throw new ValidationException("O nome da conexão deve ter no máximo 120 caracteres.");
        }

        var membership = await db.HouseholdMembers
            .Include(member => member.Household)
            .FirstOrDefaultAsync(member =>
                member.UserId == userContext.UserId &&
                member.HouseholdId == request.HouseholdId &&
                member.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a esta casa.");

        var keyId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var token = $"hpit_{keyId}_{secret}";

        var connection = new IntegrationConnection
        {
            UserId = userContext.UserId,
            HouseholdId = membership.HouseholdId,
            Name = name,
            AccessMode = request.AccessMode,
            KeyId = keyId,
            SecretHash = HashSecret(secret),
            TokenPrefix = token[..Math.Min(token.Length, 17)],
            ExpiresAt = expiresAt
        };

        db.IntegrationConnections.Add(connection);
        await db.SaveChangesAsync(cancellationToken);

        var dto = ToDto(connection, membership.Household!.Name, now);
        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        return new CreatedIntegrationConnectionDto(dto, token, $"{baseUrl}/api/integrations/v1", $"{baseUrl}/mcp");
    }

    public async Task RevokeCurrentUserConnectionAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await db.IntegrationConnections
            .FirstOrDefaultAsync(item => item.Id == connectionId && item.UserId == userContext.UserId, cancellationToken)
            ?? throw new NotFoundException("Conexão não encontrada.");

        if (!connection.RevokedAt.HasValue)
        {
            connection.RevokedAt = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IntegrationSpaceDto> GetCurrentSpaceAsync(CancellationToken cancellationToken)
    {
        if (!userContext.IsIntegration || userContext.HouseholdId is not Guid householdId)
        {
            throw new ForbiddenException("Esta operação exige uma conexão de integração.");
        }

        var membership = await db.HouseholdMembers
            .Include(member => member.Household)
            .AsNoTracking()
            .FirstOrDefaultAsync(member =>
                member.UserId == userContext.UserId &&
                member.HouseholdId == householdId &&
                member.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a esta casa.");

        var connection = await db.IntegrationConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userContext.IntegrationConnectionId, cancellationToken)
            ?? throw new UnauthorizedException("Conexão inválida.");

        return new IntegrationSpaceDto(
            householdId,
            membership.Household!.Name,
            membership.Role.ToString(),
            connection.AccessMode,
            connection.ExpiresAt,
            "America/Sao_Paulo",
            "1.0.0");
    }

    public async Task<IntegrationAuthenticatedPrincipal?> AuthenticateAsync(string token, CancellationToken cancellationToken)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.TokenPepper) || !TryParseToken(token, out var keyId, out var secret))
        {
            return null;
        }

        var connection = await db.IntegrationConnections
            .Include(item => item.User)
            .Include(item => item.Household)
            .FirstOrDefaultAsync(item => item.KeyId == keyId && item.CredentialKind == IntegrationCredentialKind.ApiKey, cancellationToken);

        if (connection?.User is null ||
            connection.IsRevoked || connection.ExpiresAt <= timeProvider.GetUtcNow() ||
            !connection.User.IsActive || !FixedTimeEquals(connection.SecretHash, HashSecret(secret)))
        {
            return null;
        }

        var membership = await db.HouseholdMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(member =>
                member.UserId == connection.UserId &&
                member.HouseholdId == connection.HouseholdId &&
                member.IsActive,
                cancellationToken);

        if (membership is null || connection.User.SystemRole == SystemRole.SuperAdmin)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (!connection.LastUsedAt.HasValue || connection.LastUsedAt.Value.AddMinutes(5) <= now)
        {
            connection.LastUsedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return new IntegrationAuthenticatedPrincipal(
            connection.Id,
            connection.UserId,
            connection.HouseholdId,
            connection.User.DisplayName,
            connection.User.SystemRole.ToString(),
            membership.Role.ToString(),
            connection.AccessMode);
    }

    private string HashSecret(string secret)
    {
        var key = Encoding.UTF8.GetBytes(options.TokenPepper);
        var data = Encoding.UTF8.GetBytes(secret);
        return Convert.ToHexString(HMACSHA256.HashData(key, data));
    }

    private void EnsureTokenPepperConfigured()
    {
        if (options.TokenPepper.Length < 32)
        {
            throw new ValidationException("A integração exige a configuração segura de Integrations:TokenPepper.");
        }
    }

    private static bool FixedTimeEquals(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
    }

    private static bool TryParseToken(string token, out string keyId, out string secret)
    {
        keyId = string.Empty;
        secret = string.Empty;
        var parts = token.Split('_', StringSplitOptions.None);
        if (parts.Length != 3 || parts[0] != "hpit" || parts[1].Length != 16 || parts[2].Length < 40)
        {
            return false;
        }

        keyId = parts[1];
        secret = parts[2];
        return true;
    }

    private static string RequiredText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message);
        }

        return value.Trim();
    }

    private static IntegrationConnectionDto ToDto(IntegrationConnection connection, string householdName, DateTimeOffset now)
        => new(
            connection.Id,
            connection.Name,
            connection.CredentialKind,
            connection.AccessMode,
            connection.HouseholdId,
            householdName,
            connection.TokenPrefix,
            connection.ExpiresAt,
            connection.RevokedAt,
            connection.LastUsedAt,
            connection.CreatedAt,
            !connection.RevokedAt.HasValue && connection.ExpiresAt > now);
}
