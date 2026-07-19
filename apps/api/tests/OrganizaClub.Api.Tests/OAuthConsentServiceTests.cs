using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using OrganizaClub.Api.Integrations;
using OrganizaClub.Api.Security;
using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Integrations;
using OrganizaClub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class OAuthConsentServiceTests
{
    private const string EncryptionKey = "RkVEQ0JBOTg3NjU0MzIxMEZFRENCQTk4NzY1NDMyMTA=";

    [Fact]
    public async Task Read_only_approval_preserves_oidc_scopes_and_removes_write()
    {
        await using var db = CreateDbContext();
        var (service, token) = await CreateServiceAsync(db, "openid offline_access organiza.read organiza.write");

        await service.ApproveAsync(token, new ApproveOAuthConsentRequest(
            (await db.Spaces.SingleAsync()).Id,
            IntegrationAccessMode.ReadOnly,
            DateTimeOffset.UtcNow.AddDays(30)), CancellationToken.None);

        var interaction = await db.OAuthAuthorizationInteractions.Include(item => item.IntegrationConnection).SingleAsync();
        Assert.Equal("openid offline_access organiza.read", interaction.Scope);
        Assert.Equal(IntegrationAccessMode.ReadOnly, interaction.IntegrationConnection!.AccessMode);
    }

    [Fact]
    public async Task Read_write_approval_preserves_all_requested_scopes()
    {
        await using var db = CreateDbContext();
        var (service, token) = await CreateServiceAsync(db, "openid offline_access organiza.read organiza.write");

        await service.ApproveAsync(token, new ApproveOAuthConsentRequest(
            (await db.Spaces.SingleAsync()).Id,
            IntegrationAccessMode.ReadWrite,
            DateTimeOffset.UtcNow.AddDays(30)), CancellationToken.None);

        var interaction = await db.OAuthAuthorizationInteractions.SingleAsync();
        Assert.Equal("openid offline_access organiza.read organiza.write", interaction.Scope);
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
            new Claim("integration_space_id", Guid.NewGuid().ToString()),
            new Claim(OpenIddictConstants.Claims.Scope, OpenIddictConstants.Scopes.OpenId)
        }, "test");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new OAuthMcpAuthorizationHandler(db, TimeProvider.System).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static OrganizaClubDbContext CreateDbContext() => new(new DbContextOptionsBuilder<OrganizaClubDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private static async Task<(OAuthConsentService Service, string Token)> CreateServiceAsync(OrganizaClubDbContext db, string scope)
    {
        var user = new AppUser
        {
            Email = "oauth@organiza.club",
            PasswordHash = "hash",
            DisplayName = "OAuth",
            SystemRole = SystemRole.User
        };
        var space = new Space { Name = "Espaço OAuth", CreatedByUserId = user.Id };
        db.AddRange(user, space, new SpaceMember
        {
            User = user,
            Space = space,
            Role = SpaceRole.Owner
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
            Resource = "https://api.organiza.club/mcp",
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
                Issuer = "https://api.organiza.club",
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
        public Guid? SpaceId => null;
    }
}
