using HomePit.Application.Common;
using HomePit.Application.Integrations;
using HomePit.Domain.Households;
using HomePit.Domain.Integrations;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class IntegrationConnectionServiceTests
{
    [Fact]
    public async Task Connection_secret_authenticates_once_and_revocation_is_immediate()
    {
        await using var db = CreateDbContext();
        var (user, household) = await SeedAsync(db);
        var context = new TestUserContext(user.Id, household.Id);
        var service = CreateService(db, context);

        var created = await service.CreateAsync(new CreateIntegrationConnectionRequest(
            "Automação", household.Id, IntegrationAccessMode.ReadWrite, DateTimeOffset.UtcNow.AddDays(90)), CancellationToken.None);

        Assert.StartsWith("hpit_", created.Token);
        Assert.Equal(IntegrationAccessMode.ReadWrite, created.Connection.AccessMode);
        var persisted = await db.IntegrationConnections.Include(item => item.User).Include(item => item.Household).SingleAsync();
        Assert.Equal(user.Id, persisted.UserId);
        Assert.Equal(household.Id, persisted.HouseholdId);
        Assert.True(persisted.User!.IsActive);
        Assert.NotNull(persisted.Household);
        var membership = await db.HouseholdMembers.SingleAsync();
        Assert.Equal(user.Id, membership.UserId);
        Assert.Equal(household.Id, membership.HouseholdId);
        Assert.True(membership.IsActive);
        Assert.NotNull(await service.AuthenticateAsync(created.Token, CancellationToken.None));

        await service.RevokeCurrentUserConnectionAsync(created.Connection.Id, CancellationToken.None);

        Assert.Null(await service.AuthenticateAsync(created.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Connection_cannot_outlive_one_year()
    {
        await using var db = CreateDbContext();
        var (user, household) = await SeedAsync(db);
        var service = CreateService(db, new TestUserContext(user.Id, household.Id));

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new CreateIntegrationConnectionRequest(
            "Longa", household.Id, IntegrationAccessMode.ReadOnly, DateTimeOffset.UtcNow.AddDays(366)), CancellationToken.None));
    }

    [Fact]
    public async Task Inactive_membership_invalidates_connection()
    {
        await using var db = CreateDbContext();
        var (user, household) = await SeedAsync(db);
        var service = CreateService(db, new TestUserContext(user.Id, household.Id));
        var created = await service.CreateAsync(new CreateIntegrationConnectionRequest(
            "Automação", household.Id, IntegrationAccessMode.ReadOnly, DateTimeOffset.UtcNow.AddDays(90)), CancellationToken.None);

        db.HouseholdMembers.Single(item => item.UserId == user.Id).IsActive = false;
        await db.SaveChangesAsync();

        Assert.Null(await service.AuthenticateAsync(created.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Token_with_base64url_underscore_authenticates()
    {
        await using var db = CreateDbContext();
        var (user, household) = await SeedAsync(db);
        const string pepper = "test-integration-pepper-with-at-least-32-characters";
        const string keyId = "81d20149c8285f2b";
        const string secret = "d8_SzyjL6cUej0zIhT2Hj2Obe9v-DM0WqaNcNYPL3Y0";
        db.IntegrationConnections.Add(new IntegrationConnection
        {
            UserId = user.Id,
            HouseholdId = household.Id,
            Name = "Chave Base64URL",
            KeyId = keyId,
            SecretHash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes(secret))),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new TestUserContext(user.Id, household.Id), pepper);

        Assert.NotNull(await service.AuthenticateAsync($"hpit_{keyId}_{secret}", CancellationToken.None));
    }

    private static IntegrationConnectionService CreateService(
        HomePitDbContext db,
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

    private static HomePitDbContext CreateDbContext() => new(new DbContextOptionsBuilder<HomePitDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private static async Task<(AppUser User, Household Household)> SeedAsync(HomePitDbContext db)
    {
        var user = new AppUser { Email = "integration@homepit.dev", PasswordHash = "hash", DisplayName = "Integration", SystemRole = SystemRole.User };
        var household = new Household { Name = "Casa de integração", CreatedByUserId = user.Id };
        db.AddRange(user, household, new HouseholdMember { User = user, Household = household, Role = HouseholdRole.Owner });
        await db.SaveChangesAsync();
        return (user, household);
    }

    private sealed class TestUserContext(Guid userId, Guid householdId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? HouseholdId { get; } = householdId;
    }
}
