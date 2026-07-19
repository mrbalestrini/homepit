using System.Security.Cryptography;
using System.Text;
using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Integrations;
using OrganizaClub.Infrastructure.Data;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace OrganizaClub.Api.Integrations;

public sealed class OAuthConsentService(
    OrganizaClubDbContext db,
    IUserContext userContext,
    IOpenIddictApplicationManager applications,
    TimeProvider timeProvider,
    IOptions<OAuthOptions> options)
{
    private static readonly HashSet<string> AllowedScopes = new(StringComparer.Ordinal)
    {
        OpenIddictConstants.Scopes.OpenId,
        OpenIddictConstants.Scopes.OfflineAccess,
        "organiza.read",
        "organiza.write"
    };

    private readonly OAuthOptions options = options.Value;

    public async Task<string> StartAsync(OpenIddictRequest request, CancellationToken cancellationToken)
    {
        EnsureConfiguration();
        ValidateAuthorizationRequest(request);

        var application = await applications.FindByClientIdAsync(request.ClientId!, cancellationToken)
            ?? throw new ValidationException("Cliente OAuth não encontrado.");
        var displayName = await applications.GetDisplayNameAsync(application, cancellationToken) ?? request.ClientId!;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = timeProvider.GetUtcNow();
        var scopes = request.GetScopes().Order(StringComparer.Ordinal).ToArray();

        // Pending authorization details are intentionally short-lived. Removing
        // expired rows here keeps the table free from authorization request data
        // even when no background scheduler is running.
        var expired = await db.OAuthAuthorizationInteractions
            .Where(item => item.ExpiresAt <= now)
            .ToArrayAsync(cancellationToken);
        db.OAuthAuthorizationInteractions.RemoveRange(expired);

        db.OAuthAuthorizationInteractions.Add(new OAuthAuthorizationInteraction
        {
            TokenHash = Hash(token),
            ClientId = request.ClientId!,
            ClientName = displayName[..Math.Min(displayName.Length, 160)],
            RedirectUri = request.RedirectUri!,
            Scope = string.Join(' ', scopes),
            State = request.State,
            CodeChallenge = request.CodeChallenge!,
            CodeChallengeMethod = request.CodeChallengeMethod!,
            Resource = GetResource(request),
            ExpiresAt = now.AddMinutes(options.InteractionMinutes)
        });
        await db.SaveChangesAsync(cancellationToken);

        var separator = options.WebConsentUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{options.WebConsentUrl}{separator}interaction={Uri.EscapeDataString(token)}";
    }

    public async Task<OAuthConsentInteractionDto> GetAsync(string token, CancellationToken cancellationToken)
    {
        var interaction = await FindActiveAsync(token, cancellationToken);
        return new OAuthConsentInteractionDto(interaction.ClientName, GetScopes(interaction), interaction.ExpiresAt);
    }

    public async Task<OAuthConsentContinuationDto> ApproveAsync(
        string token,
        ApproveOAuthConsentRequest request,
        CancellationToken cancellationToken)
    {
        EnsureCurrentUserCanAuthorize();
        var interaction = await FindActiveAsync(token, cancellationToken);
        var requestedScopes = GetScopes(interaction);
        var expiresAt = request.ExpiresAt.ToUniversalTime();
        var now = timeProvider.GetUtcNow();
        if (expiresAt <= now.AddDays(1) || expiresAt > now.AddDays(365))
        {
            throw new ValidationException("A expiração deve ficar entre amanhã e os próximos 365 dias.");
        }

        if (request.AccessMode == IntegrationAccessMode.ReadWrite && !requestedScopes.Contains("organiza.write"))
        {
            throw new ValidationException("O cliente não solicitou acesso de escrita.");
        }

        var membership = await db.SpaceMembers
            .Include(item => item.Space)
            .FirstOrDefaultAsync(item => item.UserId == userContext.UserId && item.SpaceId == request.SpaceId && item.IsActive, cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a este espaço.");

        var grantedScopes = requestedScopes
            .Where(scope => scope != "organiza.write" || request.AccessMode == IntegrationAccessMode.ReadWrite)
            .ToArray();
        var connection = new IntegrationConnection
        {
            UserId = userContext.UserId,
            SpaceId = membership.SpaceId,
            Name = interaction.ClientName,
            CredentialKind = IntegrationCredentialKind.OAuthGrant,
            AccessMode = request.AccessMode,
            ExpiresAt = expiresAt
        };

        db.IntegrationConnections.Add(connection);
        interaction.ApprovedAt = now;
        interaction.ApprovedByUserId = userContext.UserId;
        interaction.IntegrationConnection = connection;
        interaction.Scope = string.Join(' ', grantedScopes);
        await db.SaveChangesAsync(cancellationToken);

        return new OAuthConsentContinuationDto(BuildContinuationUrl(interaction, token));
    }

    public async Task<OAuthConsentContinuationDto> DenyAsync(string token, CancellationToken cancellationToken)
    {
        EnsureCurrentUserCanAuthorize();
        var interaction = await FindActiveAsync(token, cancellationToken);
        interaction.DeniedAt = timeProvider.GetUtcNow();
        interaction.ApprovedByUserId = userContext.UserId;
        await db.SaveChangesAsync(cancellationToken);
        return new OAuthConsentContinuationDto(BuildContinuationUrl(interaction, token));
    }

    public async Task<OAuthAuthorizationInteraction> ConsumeApprovedAsync(
        OpenIddictRequest request,
        string token,
        CancellationToken cancellationToken)
    {
        var interaction = await db.OAuthAuthorizationInteractions
            .Include(item => item.IntegrationConnection)
            .FirstOrDefaultAsync(item => item.TokenHash == Hash(token), cancellationToken)
            ?? throw new UnauthorizedException("A autorização OAuth expirou ou não é válida.");

        if (interaction.ExpiresAt <= timeProvider.GetUtcNow() || interaction.ConsumedAt.HasValue || !interaction.ApprovedAt.HasValue ||
            interaction.DeniedAt.HasValue || interaction.IntegrationConnection is null || !Matches(interaction, request))
        {
            throw new UnauthorizedException("A autorização OAuth expirou ou não é válida.");
        }

        interaction.ConsumedAt = timeProvider.GetUtcNow();
        db.OAuthAuthorizationInteractions.Remove(interaction);
        await db.SaveChangesAsync(cancellationToken);
        return interaction;
    }

    public static string GetResource(OpenIddictRequest request) => request.GetParameter("resource")?.ToString() ?? string.Empty;

    private async Task<OAuthAuthorizationInteraction> FindActiveAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new NotFoundException("Solicitação de autorização não encontrada.");
        }

        var interaction = await db.OAuthAuthorizationInteractions
            .FirstOrDefaultAsync(item => item.TokenHash == Hash(token), cancellationToken)
            ?? throw new NotFoundException("Solicitação de autorização não encontrada.");
        if (interaction.ExpiresAt <= timeProvider.GetUtcNow())
        {
            db.OAuthAuthorizationInteractions.Remove(interaction);
            await db.SaveChangesAsync(cancellationToken);
            throw new NotFoundException("Solicitação de autorização não está mais disponível.");
        }

        if (interaction.ConsumedAt.HasValue || interaction.ApprovedAt.HasValue || interaction.DeniedAt.HasValue)
        {
            throw new NotFoundException("Solicitação de autorização não está mais disponível.");
        }

        return interaction;
    }

    private void ValidateAuthorizationRequest(OpenIddictRequest request)
    {
        if (!string.Equals(request.ResponseType, OpenIddictConstants.ResponseTypes.Code, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.RedirectUri) ||
            string.IsNullOrWhiteSpace(request.CodeChallenge) ||
            !string.Equals(request.CodeChallengeMethod, "S256", StringComparison.Ordinal) ||
            !string.Equals(GetResource(request), options.CanonicalMcpResource, StringComparison.Ordinal))
        {
            throw new ValidationException("A solicitação OAuth é inválida ou não está vinculada ao MCP Organiza Club.");
        }

        var scopes = request.GetScopes().ToArray();
        if (!scopes.Contains("organiza.read") || scopes.Any(scope => !AllowedScopes.Contains(scope)))
        {
            throw new ValidationException("Os escopos OAuth solicitados não são permitidos.");
        }
    }

    private static IReadOnlyCollection<string> GetScopes(OAuthAuthorizationInteraction interaction) =>
        interaction.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool Matches(OAuthAuthorizationInteraction interaction, OpenIddictRequest request) =>
        string.Equals(interaction.ClientId, request.ClientId, StringComparison.Ordinal) &&
        string.Equals(interaction.RedirectUri, request.RedirectUri, StringComparison.Ordinal) &&
        string.Equals(interaction.State, request.State, StringComparison.Ordinal) &&
        string.Equals(interaction.CodeChallenge, request.CodeChallenge, StringComparison.Ordinal) &&
        string.Equals(interaction.CodeChallengeMethod, request.CodeChallengeMethod, StringComparison.Ordinal) &&
        string.Equals(interaction.Resource, GetResource(request), StringComparison.Ordinal) &&
        string.Equals(interaction.Scope, string.Join(' ', request.GetScopes().Order(StringComparer.Ordinal)), StringComparison.Ordinal);

    private string BuildContinuationUrl(OAuthAuthorizationInteraction interaction, string token)
    {
        var values = new Dictionary<string, string?>
        {
            ["response_type"] = OpenIddictConstants.ResponseTypes.Code,
            ["client_id"] = interaction.ClientId,
            ["redirect_uri"] = interaction.RedirectUri,
            ["scope"] = interaction.Scope,
            ["state"] = interaction.State,
            ["code_challenge"] = interaction.CodeChallenge,
            ["code_challenge_method"] = interaction.CodeChallengeMethod,
            ["resource"] = interaction.Resource,
            ["interaction"] = token
        };
        return $"{options.Issuer.TrimEnd('/')}/connect/authorize{QueryString.Create(values)}";
    }

    private void EnsureCurrentUserCanAuthorize()
    {
        if (userContext.UserId == Guid.Empty || userContext.SystemRole == SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Esta conta não pode autorizar conexões OAuth.");
        }
    }

    private void EnsureConfiguration()
    {
        if (options.InteractionMinutes is < 1 or > 30 || string.IsNullOrWhiteSpace(options.EncryptionKey))
        {
            throw new ValidationException("OAuth não está configurado com segurança.");
        }
    }

    private string Hash(string value)
    {
        var key = Convert.FromBase64String(options.EncryptionKey);
        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));
    }
}
