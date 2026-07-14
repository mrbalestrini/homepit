using System.Security.Claims;
using System.Text.Encodings.Web;
using HomePit.Application.Integrations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HomePit.Api.Security;

public sealed class IntegrationTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IntegrationConnectionService connections)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer hpit_", StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var principal = await connections.AuthenticateAsync(authorization["Bearer ".Length..], Context.RequestAborted);
        if (principal is null)
        {
            return AuthenticateResult.Fail("Chave de integração inválida ou expirada.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, principal.UserId.ToString()),
            new Claim(ClaimTypes.Name, principal.DisplayName),
            new Claim("system_role", principal.SystemRole),
            new Claim("household", principal.HouseholdId.ToString()),
            new Claim($"household:{principal.HouseholdId}:role", principal.HouseholdRole),
            new Claim("integration", bool.TrueString),
            new Claim("integration_connection_id", principal.ConnectionId.ToString()),
            new Claim("integration_household_id", principal.HouseholdId.ToString()),
            new Claim("integration_access_mode", principal.AccessMode.ToString())
        };
        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
