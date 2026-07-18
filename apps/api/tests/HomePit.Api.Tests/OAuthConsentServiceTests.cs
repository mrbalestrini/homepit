using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HomePit.Api.Integrations;
using HomePit.Api.Security;
using HomePit.Application.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Integrations;
using HomePit.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class OAuthConsentServiceTests
{
    private const string EncryptionKey = "RkVEQ0JBOTg3NjU0MzIxMEZFRENCQTk4NzY1NDMyMTA=";

    [Fact]
    public async Task Read_only_approval_preserves_oidc_scopes_and_removes_write()
    {
        await using var db = CreateDbContext();
        var (service, token) = await CreateServiceAsync(db, "openid offline_access homepit.read homepit.write");

        await service.ApproveAsync(token, new ApproveOAuthConsentRequest(
            (await db.Households.SingleAsync()).Id,
            IntegrationAccessMode.ReadOnly,
            DateTimeOffset.UtcNow.AddDays(30)), CancellationToken.None);

        var interaction = await db.OAuthAuthorizationInteractions.Include(item => item.IntegrationConnection).SingleAsync();
        Assert.Equal("openid offline_access homepit.read", interaction.Scope);
        Assert.Equal(IntegrationAccessMode.ReadOnly, interaction.IntegrationConnection!.AccessMode);
    }

    [Fact]
    public async Task Read_write_approval_preserves_all_requested_scopes()
    {
        await using var db = CreateDbContext();
        var (service, token) = await CreateServiceAsync(db, "openid offline_access homepit.read homepit.write");

        await service.ApproveAsync(token, new ApproveOAuthConsentRequest(
            (await db.Households.SingleAsync()).Id,
            IntegrationAccessMode.ReadWrite,
            DateTimeOffset.UtcNow.AddDays(30)), CancellationToken.None);

        var interaction = await db.OAuthAuthorizationInteractions.SingleAsync();
        Assert.Equal("openid offline_access homepit.read homepit.write", interaction.Scope);
    }

    [Fact]
    public async Task Openid_alone_never_satisfies_the_mcp_requirement()
    {
        await using var db = CreateDbContext();
        var requirement = new OAuthMcpRequirement();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, Guid.NewGuid().ToString()),
            new Claim("integration_connection_id", Guid.NewGuid().ToString()),
            new Claim("integration_household_id", Guid.NewGuid().ToString()),
            new Claim(OpenIddictConstants.Claims.Scope, OpenIddictConstants.Scopes.OpenId)
        }, "test");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new OAuthMcpAuthorizationHandler(db, TimeProvider.System).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static HomePitDbContext CreateDbContext() => new(new DbContextOptionsBuilder<HomePitDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private static async Task<(OAuthConsentService Service, string Token)> CreateServiceAsync(HomePitDbContext db, string scope)
    {
        var user = new AppUser
        {
            Email = "oauth@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "OAuth",
            SystemRole = SystemRole.User
        };
        var household = new Household { Name = "Casa OAuth", CreatedByUserId = user.Id };
        db.AddRange(user, household, new HouseholdMember
        {
            User = user,
            Household = household,
            Role = HouseholdRole.Owner
        });
        const string token = "approval-token";
        db.OAuthAuthorizationInteractions.Add(new OAuthAuthorizationInteraction
        {
            TokenHash = Hash(token),
            ClientId = "mcp_client",
            ClientName = "Cliente MCP",
            RedirectUri = "http://localhost:6274/oauth/callback",
            Scope = scope,
            CodeChallenge = "challenge",
            CodeChallengeMethod = "S256",
            Resource = "https://api.homepit.dev/mcp",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        });
        await db.SaveChangesAsync();

        var service = new OAuthConsentService(
            db,
            new TestUserContext(user.Id),
            null!,
            TimeProvider.System,
            Options.Create(new OAuthOptions
            {
                Issuer = "https://api.homepit.dev",
                EncryptionKey = EncryptionKey
            }));
        return (service, token);
    }

    private static string Hash(string value) => Convert.ToHexString(HMACSHA256.HashData(
        Convert.FromBase64String(EncryptionKey), Encoding.UTF8.GetBytes(value)));

    private sealed class TestUserContext(Guid userId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? HouseholdId => null;
    }
}
