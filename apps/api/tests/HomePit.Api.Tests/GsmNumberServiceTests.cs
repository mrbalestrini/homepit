using HomePit.Application.Common;
using HomePit.Application.Gsm;
using HomePit.Domain.Gsm;
using HomePit.Domain.Households;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class GsmNumberServiceTests
{
    [Fact]
    public async Task Create_normalizes_number_with_default_ddi()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var created = await service.CreateAsync(
            new CreateGsmNumberRequest(
                "Chip principal",
                "(11) 91234-5678",
                "Recarga mensal",
                GsmNumberPlan.PrePago,
                59.9m,
                new DateOnly(2026, 1, 10),
                new DateOnly(2026, 6, 20),
                GsmNumberStatus.Ativo),
            CancellationToken.None);

        Assert.Equal("5511912345678", created.Number);
        Assert.Equal(GsmNumberStatus.Ativo, created.Status);
        Assert.Equal(GsmNumberPlan.PrePago, created.Plan);
        Assert.Equal(59.9m, created.MonthlyCost);
    }

    [Fact]
    public async Task Create_preserves_explicit_ddi()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var created = await service.CreateAsync(
            new CreateGsmNumberRequest(
                "Linha externa",
                "+44 (11) 91234-5678",
                null,
                GsmNumberPlan.PosPago,
                null,
                new DateOnly(2026, 2, 1),
                null,
                GsmNumberStatus.Inativo),
            CancellationToken.None);

        Assert.Equal("4411912345678", created.Number);
        Assert.Equal(GsmNumberStatus.Inativo, created.Status);
        Assert.Equal(GsmNumberPlan.PosPago, created.Plan);
        Assert.Null(created.MonthlyCost);
    }

    [Fact]
    public async Task Create_rejects_invalid_number_length()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new CreateGsmNumberRequest(
                "Inválido",
                "1234567890",
                null,
                GsmNumberPlan.PrePago,
                null,
                new DateOnly(2026, 2, 1),
                null,
                GsmNumberStatus.Ativo),
            CancellationToken.None));

        Assert.Equal("Informe um número GSM válido com DDI opcional e DDD obrigatório.", exception.Message);
    }

    [Fact]
    public async Task Create_rejects_last_recharge_before_acquisition()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new CreateGsmNumberRequest(
                "Linha com datas inválidas",
                "11912345678",
                null,
                GsmNumberPlan.PrePago,
                null,
                new DateOnly(2026, 6, 10),
                new DateOnly(2026, 6, 1),
                GsmNumberStatus.Ativo),
            CancellationToken.None));

        Assert.Equal("A data da última recarga não pode ser anterior à data de aquisição.", exception.Message);
    }

    [Fact]
    public async Task Create_enforces_uniqueness_per_household()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        await service.CreateAsync(
            new CreateGsmNumberRequest(
                "Linha 1",
                "11912345678",
                null,
                GsmNumberPlan.PrePago,
                null,
                new DateOnly(2026, 1, 10),
                null,
                GsmNumberStatus.Ativo),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new CreateGsmNumberRequest(
                "Linha 2",
                "+55 (11) 91234-5678",
                null,
                GsmNumberPlan.PrePago,
                null,
                new DateOnly(2026, 1, 11),
                null,
                GsmNumberStatus.Ativo),
            CancellationToken.None));

        Assert.Equal("Este número GSM já está cadastrado nesta casa.", exception.Message);
    }

    [Fact]
    public async Task Member_cannot_update_gsm_number_created_by_someone_else()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);

        var number = new GsmNumber
        {
            HouseholdId = fixture.HouseholdId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Linha da casa",
            NormalizedNumber = "5511912345678",
            AcquiredOn = new DateOnly(2026, 1, 1),
            Plan = GsmNumberPlan.PrePago,
            Status = GsmNumberStatus.Ativo
        };
        context.GsmNumbers.Add(number);
        await context.SaveChangesAsync();

        var service = CreateService(context, fixture.MemberUserId, fixture.HouseholdId);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(
            number.Id,
            new UpdateGsmNumberRequest(
                "Linha atualizada",
                "11999998888",
                null,
                GsmNumberPlan.PosPago,
                42.5m,
                new DateOnly(2026, 1, 1),
                null,
                GsmNumberStatus.Inativo),
            CancellationToken.None));
    }

    [Fact]
    public async Task Superadmin_cannot_create_gsm_number()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, Guid.NewGuid(), fixture.HouseholdId, SystemRole.SuperAdmin);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            new CreateGsmNumberRequest(
                "Linha SA",
                "11912345678",
                null,
                GsmNumberPlan.PrePago,
                null,
                new DateOnly(2026, 1, 1),
                null,
                GsmNumberStatus.Ativo),
            CancellationToken.None));

        Assert.Equal("O superadmin possui acesso somente leitura nesta etapa.", exception.Message);
    }

    [Fact]
    public async Task Create_rejects_negative_monthly_cost()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new CreateGsmNumberRequest(
                "Linha negativa",
                "11912345678",
                null,
                GsmNumberPlan.PrePago,
                -1m,
                new DateOnly(2026, 1, 1),
                null,
                GsmNumberStatus.Ativo),
            CancellationToken.None));

        Assert.Equal("O custo mensal da linha não pode ser negativo.", exception.Message);
    }

    private static HomePitDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomePitDbContext(options);
    }

    private static GsmNumberService CreateService(
        HomePitDbContext context,
        Guid userId,
        Guid? householdId,
        SystemRole systemRole = SystemRole.User)
    {
        return new GsmNumberService(
            context,
            new TestUserContext(userId, householdId, systemRole),
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 23, 12, 0, 0, TimeSpan.Zero)));
    }

    private static async Task<Fixture> SeedFixtureAsync(HomePitDbContext context)
    {
        var ownerUser = new AppUser
        {
            Email = "owner-gsm@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var memberUser = new AppUser
        {
            Email = "member-gsm@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Member",
            SystemRole = SystemRole.User
        };
        var household = new Household
        {
            Name = "Casa GSM"
        };
        var ownerMember = new HouseholdMember
        {
            Household = household,
            User = ownerUser,
            Role = HouseholdRole.Owner
        };
        var member = new HouseholdMember
        {
            Household = household,
            User = memberUser,
            Role = HouseholdRole.Member
        };

        context.Users.AddRange(ownerUser, memberUser);
        context.Households.Add(household);
        context.HouseholdMembers.AddRange(ownerMember, member);
        await context.SaveChangesAsync();

        return new Fixture(household.Id, ownerUser.Id, ownerMember.Id, memberUser.Id);
    }

    private sealed record Fixture(Guid HouseholdId, Guid OwnerUserId, Guid OwnerMemberId, Guid MemberUserId);

    private sealed class TestUserContext(Guid userId, Guid? householdId, SystemRole systemRole) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole { get; } = systemRole;
        public Guid? HouseholdId { get; } = householdId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
