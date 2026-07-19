using OrganizaClub.Application.Common;
using OrganizaClub.Application.Integrations;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Integrations;
using OrganizaClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class IntegrationConnectionServiceTests
{
    [Fact]
    public async Task Connection_secret_authenticates_once_and_revocation_is_immediate()
    {
        await using var db = CreateDbContext();
        var (user, space) = await SeedAsync(db);
        var context = new TestUserContext(user.Id, space.Id);
        var service = CreateService(db, context);

        var created = await service.CreateAsync(new CreateIntegrationConnectionRequest(
            "Automação", space.Id, IntegrationAccessMode.ReadWrite, DateTimeOffset.UtcNow.AddDays(90)), CancellationToken.None);

        Assert.StartsWith("orgc_", created.Token);
        Assert.Equal(IntegrationAccessMode.ReadWrite, created.Connection.AccessMode);
        var persisted = await db.IntegrationConnections.Include(item => item.User).Include(item => item.Space).SingleAsync();
        Assert.Equal(user.Id, persisted.UserId);
        Assert.Equal(space.Id, persisted.SpaceId);
        Assert.True(persisted.User!.IsActive);
        Assert.NotNull(persisted.Space);
        var membership = await db.SpaceMembers.SingleAsync();
        Assert.Equal(user.Id, membership.UserId);
        Assert.Equal(space.Id, membership.SpaceId);
        Assert.True(membership.IsActive);
        Assert.NotNull(await service.AuthenticateAsync(created.Token, CancellationToken.None));

        await service.RevokeCurrentUserConnectionAsync(created.Connection.Id, CancellationToken.None);

        Assert.Null(await service.AuthenticateAsync(created.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Connection_cannot_outlive_one_year()
    {
        await using var db = CreateDbContext();
        var (user, space) = await SeedAsync(db);
        var service = CreateService(db, new TestUserContext(user.Id, space.Id));

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new CreateIntegrationConnectionRequest(
            "Longa", space.Id, IntegrationAccessMode.ReadOnly, DateTimeOffset.UtcNow.AddDays(366)), CancellationToken.None));
    }

    [Fact]
    public async Task Inactive_membership_invalidates_connection()
    {
        await using var db = CreateDbContext();
        var (user, space) = await SeedAsync(db);
        var service = CreateService(db, new TestUserContext(user.Id, space.Id));
        var created = await service.CreateAsync(new CreateIntegrationConnectionRequest(
            "Automação", space.Id, IntegrationAccessMode.ReadOnly, DateTimeOffset.UtcNow.AddDays(90)), CancellationToken.None);

        db.SpaceMembers.Single(item => item.UserId == user.Id).IsActive = false;
        await db.SaveChangesAsync();

        Assert.Null(await service.AuthenticateAsync(created.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Token_with_base64url_underscore_authenticates()
    {
        await using var db = CreateDbContext();
        var (user, space) = await SeedAsync(db);
        const string pepper = "test-integration-pepper-with-at-least-32-characters";
        const string keyId = "81d20149c8285f2b";
        const string secret = "d8_SzyjL6cUej0zIhT2Hj2Obe9v-DM0WqaNcNYPL3Y0";
        db.IntegrationConnections.Add(new IntegrationConnection
        {
            UserId = user.Id,
            SpaceId = space.Id,
            Name = "Chave Base64URL",
            KeyId = keyId,
            SecretHash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes(secret))),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new TestUserContext(user.Id, space.Id), pepper);

        Assert.NotNull(await service.AuthenticateAsync($"orgc_{keyId}_{secret}", CancellationToken.None));
    }

    private static IntegrationConnectionService CreateService(
        OrganizaClubDbContext db,
        TestUserContext context,
        string pepper = "test-integration-pepper-with-at-least-32-characters") => new(
        db,
        context,
        TimeProvider.System,
        Options.Create(new IntegrationOptions
        {
            Enabled = true,
            TokenPepper = pepper
        }));

    private static OrganizaClubDbContext CreateDbContext() => new(new DbContextOptionsBuilder<OrganizaClubDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private static async Task<(AppUser User, Space Space)> SeedAsync(OrganizaClubDbContext db)
    {
        var user = new AppUser { Email = "integration@organiza.club", PasswordHash = "hash", DisplayName = "Integration", SystemRole = SystemRole.User };
        var space = new Space { Name = "Espaço de integração", CreatedByUserId = user.Id };
        db.AddRange(user, space, new SpaceMember { User = user, Space = space, Role = SpaceRole.Owner });
        await db.SaveChangesAsync();
        return (user, space);
    }

    private sealed class TestUserContext(Guid userId, Guid spaceId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? SpaceId { get; } = spaceId;
    }
}
