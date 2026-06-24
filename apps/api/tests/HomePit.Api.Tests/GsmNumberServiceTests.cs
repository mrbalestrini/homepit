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
                30,
                new DateOnly(2026, 1, 10),
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
                null,
                new DateOnly(2026, 2, 1),
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
                null,
                new DateOnly(2026, 2, 1),
                GsmNumberStatus.Ativo),
            CancellationToken.None));

        Assert.Equal("Informe um número GSM válido com DDI opcional e DDD obrigatório.", exception.Message);
    }

    [Fact]
    public async Task Create_rejects_invalid_days_without_recharge()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new CreateGsmNumberRequest(
                "Linha com prazo inválido",
                "11912345678",
                null,
                GsmNumberPlan.PrePago,
                null,
                0,
                new DateOnly(2026, 6, 10),
                GsmNumberStatus.Ativo),
            CancellationToken.None));

        Assert.Equal("Os dias possíveis sem recarga devem ser um inteiro positivo.", exception.Message);
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
                null,
                new DateOnly(2026, 1, 10),
                GsmNumberStatus.Ativo),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new CreateGsmNumberRequest(
                "Linha 2",
                "+55 (11) 91234-5678",
                null,
                GsmNumberPlan.PrePago,
                null,
                null,
                new DateOnly(2026, 1, 11),
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
                null,
                new DateOnly(2026, 1, 1),
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
                null,
                new DateOnly(2026, 1, 1),
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
                null,
                new DateOnly(2026, 1, 1),
                GsmNumberStatus.Ativo),
            CancellationToken.None));

        Assert.Equal("O custo mensal da linha não pode ser negativo.", exception.Message);
    }

    [Fact]
    public async Task Recharge_crud_updates_last_recharge_on_from_history()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var gsmNumber = new GsmNumber
        {
            HouseholdId = fixture.HouseholdId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Linha com histórico",
            NormalizedNumber = "5511912345678",
            AcquiredOn = new DateOnly(2026, 1, 1),
            Status = GsmNumberStatus.Ativo
        };
        context.GsmNumbers.Add(gsmNumber);
        await context.SaveChangesAsync();

        var first = await service.CreateRechargeAsync(
            gsmNumber.Id,
            new CreateGsmRechargeRequest(new DateOnly(2026, 6, 10), 50m, "Primeira recarga"),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 6, 10), first.RechargedOn);
        Assert.Equal(50m, first.Amount);
        Assert.Equal("Primeira recarga", first.Note);

        var second = await service.CreateRechargeAsync(
            gsmNumber.Id,
            new CreateGsmRechargeRequest(new DateOnly(2026, 6, 20), 60m, "Segunda recarga"),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 6, 20), second.RechargedOn);
        Assert.Equal(new DateOnly(2026, 6, 20), gsmNumber.LastRechargeOn);

        var history = await service.ListRechargesAsync(gsmNumber.Id, CancellationToken.None);
        Assert.Equal(2, history.Count);
        Assert.Equal(new DateOnly(2026, 6, 20), history.First().RechargedOn);

        var updated = await service.UpdateRechargeAsync(
            gsmNumber.Id,
            second.Id,
            new UpdateGsmRechargeRequest(new DateOnly(2026, 6, 22), 62.5m, "Ajustada"),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 6, 22), updated.RechargedOn);
        Assert.Equal(62.5m, updated.Amount);
        Assert.Equal("Ajustada", updated.Note);
        Assert.Equal(new DateOnly(2026, 6, 22), gsmNumber.LastRechargeOn);

        await service.DeleteRechargeAsync(gsmNumber.Id, updated.Id, CancellationToken.None);
        Assert.Equal(new DateOnly(2026, 6, 10), gsmNumber.LastRechargeOn);
    }

    [Fact]
    public async Task Member_cannot_manage_recharge_created_by_someone_else()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);

        var gsmNumber = new GsmNumber
        {
            HouseholdId = fixture.HouseholdId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Linha da casa",
            NormalizedNumber = "5511912345678",
            AcquiredOn = new DateOnly(2026, 1, 1),
            Status = GsmNumberStatus.Ativo
        };
        var recharge = new GsmRecharge
        {
            HouseholdId = fixture.HouseholdId,
            GsmNumber = gsmNumber,
            CreatedByMemberId = fixture.OwnerMemberId,
            RechargedOn = new DateOnly(2026, 6, 10),
            Amount = 50m,
            Note = "Recarga inicial"
        };
        context.GsmNumbers.Add(gsmNumber);
        context.GsmRecharges.Add(recharge);
        await context.SaveChangesAsync();

        var service = CreateService(context, fixture.MemberUserId, fixture.HouseholdId);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateRechargeAsync(
            gsmNumber.Id,
            recharge.Id,
            new UpdateGsmRechargeRequest(new DateOnly(2026, 6, 11), 55m, "Tentativa"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Create_recharge_rejects_date_before_acquisition()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var gsmNumber = new GsmNumber
        {
            HouseholdId = fixture.HouseholdId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Linha com data",
            NormalizedNumber = "5511912345678",
            AcquiredOn = new DateOnly(2026, 6, 10),
            Status = GsmNumberStatus.Ativo
        };
        context.GsmNumbers.Add(gsmNumber);
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateRechargeAsync(
            gsmNumber.Id,
            new CreateGsmRechargeRequest(new DateOnly(2026, 6, 1), 50m, "Tentativa"),
            CancellationToken.None));

        Assert.Equal("A data da recarga não pode ser anterior à data de aquisição.", exception.Message);
    }

    [Fact]
    public async Task Superadmin_cannot_create_recharge()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);

        var gsmNumber = new GsmNumber
        {
            HouseholdId = fixture.HouseholdId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Linha SA",
            NormalizedNumber = "5511912345678",
            AcquiredOn = new DateOnly(2026, 1, 1),
            Status = GsmNumberStatus.Ativo
        };
        context.GsmNumbers.Add(gsmNumber);
        await context.SaveChangesAsync();

        var service = CreateService(context, Guid.NewGuid(), fixture.HouseholdId, SystemRole.SuperAdmin);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateRechargeAsync(
            gsmNumber.Id,
            new CreateGsmRechargeRequest(new DateOnly(2026, 6, 10), 50m, "Recarga"),
            CancellationToken.None));

        Assert.Equal("O superadmin possui acesso somente leitura nesta etapa.", exception.Message);
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
