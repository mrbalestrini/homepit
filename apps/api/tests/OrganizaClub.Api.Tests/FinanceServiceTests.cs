using OrganizaClub.Application.Common;
using OrganizaClub.Application.Finance;
using OrganizaClub.Domain.Finance;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Projects;
using OrganizaClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class FinanceServiceTests
{
    [Fact]
    public async Task Generate_period_supports_missing_only_and_duplicate_all_modes()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        context.FinanceRecurringTemplates.Add(new FinanceRecurringTemplate
        {
            SpaceId = fixture.SpaceId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Condominio",
            DefaultAmount = 776.5m,
            Type = FinanceEntryType.Saida,
            Recurrence = FinanceRecurrence.Monthly,
            DayOfMonth = 10,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var firstGeneration = await service.GeneratePeriodAsync(
            2026,
            7,
            new GenerateFinancePeriodRequest("missingOnly"),
            CancellationToken.None);

        Assert.True(firstGeneration.Exists);
        Assert.Single(firstGeneration.Entries);
        Assert.Equal(FinanceEntryOrigin.RecurringTemplate, firstGeneration.Entries.Single().Origin);

        var secondGeneration = await service.GeneratePeriodAsync(
            2026,
            7,
            new GenerateFinancePeriodRequest("missingOnly"),
            CancellationToken.None);

        Assert.Single(secondGeneration.Entries);

        var duplicateGeneration = await service.GeneratePeriodAsync(
            2026,
            7,
            new GenerateFinancePeriodRequest("duplicateAll"),
            CancellationToken.None);

        Assert.Equal(2, duplicateGeneration.Entries.Count);
        Assert.All(duplicateGeneration.Entries, entry => Assert.Equal("Condominio", entry.Title));
    }

    [Fact]
    public async Task Generate_period_applies_annual_recurrence_only_on_the_matching_month()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        context.FinanceRecurringTemplates.Add(new FinanceRecurringTemplate
        {
            SpaceId = fixture.SpaceId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "IPTU",
            DefaultAmount = 1800m,
            Type = FinanceEntryType.Saida,
            Recurrence = FinanceRecurrence.Annual,
            DayOfMonth = 5,
            MonthOfYear = 7,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var july = await service.GeneratePeriodAsync(
            2026,
            7,
            new GenerateFinancePeriodRequest("missingOnly"),
            CancellationToken.None);
        var august = await service.GeneratePeriodAsync(
            2026,
            8,
            new GenerateFinancePeriodRequest("missingOnly"),
            CancellationToken.None);

        var julyEntry = Assert.Single(july.Entries);
        Assert.Equal("IPTU", julyEntry.Title);
        Assert.Equal(new DateOnly(2026, 7, 5), julyEntry.ReferenceDate);
        Assert.Empty(august.Entries);
    }

    [Fact]
    public async Task Create_entry_derives_core_from_project_and_rejects_mismatched_core()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var created = await service.CreateEntryAsync(
            new CreateFinanceEntryRequest(
                2026,
                7,
                "Compra do projeto",
                "Observacao",
                150m,
                FinanceEntryType.Saida,
                false,
                new DateOnly(2026, 7, 6),
                null,
                null,
                null,
                fixture.ProjectId),
            CancellationToken.None);

        Assert.Equal(fixture.ProjectId, created.ProjectId);
        Assert.Equal(fixture.CoreId, created.CoreId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateEntryAsync(
            new CreateFinanceEntryRequest(
                2026,
                7,
                "Compra invalida",
                null,
                90m,
                FinanceEntryType.Saida,
                false,
                new DateOnly(2026, 7, 7),
                null,
                null,
                fixture.OtherCoreId,
                fixture.ProjectId),
            CancellationToken.None));

        Assert.Equal("O projeto informado não pertence ao núcleo selecionado.", exception.Message);
    }

    [Fact]
    public async Task Closing_a_statement_creates_and_updates_the_monthly_cash_entry()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var account = await service.CreateCreditCardAccountAsync(
            new CreateCreditCardAccountRequest("Nubank", "Mastercard", "1234", 20, 25, null, true),
            CancellationToken.None);
        context.ChangeTracker.Clear();

        var firstTransaction = await service.CreateCreditCardTransactionAsync(
            account.Id,
            new CreateCreditCardTransactionRequest(
                "Mercado",
                "Mercado da esquina",
                120m,
                new DateOnly(2026, 7, 4),
                null,
                fixture.DefaultCategoryId,
                fixture.CoreId,
                fixture.ProjectId,
                null,
                null,
                null),
            CancellationToken.None);
        context.ChangeTracker.Clear();

        var secondTransaction = await service.CreateCreditCardTransactionAsync(
            account.Id,
            new CreateCreditCardTransactionRequest(
                "Farmacia",
                "Farmacia central",
                80m,
                new DateOnly(2026, 7, 8),
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);
        context.ChangeTracker.Clear();

        var createdStatement = await service.CreateCreditCardStatementAsync(
            account.Id,
            new CreateCreditCardStatementRequest(
                new DateOnly(2026, 7, 20),
                new DateOnly(2026, 7, 25),
                "Fechamento inicial",
                [firstTransaction.Id],
                null,
                null,
                null),
            CancellationToken.None);

        Assert.Equal(120m, createdStatement.TotalAmount);
        var julyAfterCreate = await service.GetPeriodAsync(2026, 7, CancellationToken.None);
        var generatedJulyEntry = Assert.Single(
            julyAfterCreate.Entries,
            entry => entry.Origin == FinanceEntryOrigin.CreditCardStatement);
        Assert.Equal(120m, generatedJulyEntry.Amount);
        Assert.True(generatedJulyEntry.Verified);
        Assert.Equal(createdStatement.FinanceEntryId, generatedJulyEntry.Id);

        var updatedStatement = await service.UpdateCreditCardStatementAsync(
            account.Id,
            createdStatement.Id,
            new UpdateCreditCardStatementRequest(
                new DateOnly(2026, 7, 20),
                new DateOnly(2026, 8, 5),
                "Fatura ajustada",
                [firstTransaction.Id, secondTransaction.Id],
                null,
                null,
                null),
            CancellationToken.None);

        Assert.Equal(200m, updatedStatement.TotalAmount);
        var julyAfterUpdate = await service.GetPeriodAsync(2026, 7, CancellationToken.None);
        Assert.DoesNotContain(julyAfterUpdate.Entries, entry => entry.Origin == FinanceEntryOrigin.CreditCardStatement);

        var augustAfterUpdate = await service.GetPeriodAsync(2026, 8, CancellationToken.None);
        var generatedAugustEntry = Assert.Single(
            augustAfterUpdate.Entries,
            entry => entry.Origin == FinanceEntryOrigin.CreditCardStatement);
        Assert.Equal(200m, generatedAugustEntry.Amount);
        Assert.True(generatedAugustEntry.Verified);
        Assert.Equal(new DateOnly(2026, 8, 5), generatedAugustEntry.ReferenceDate);
        Assert.Equal("Fatura Nubank - 08/2026", generatedAugustEntry.Title);
    }

    [Fact]
    public async Task Import_transactions_creates_missing_categories_and_reuses_existing_ones()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var account = await service.CreateCreditCardAccountAsync(
            new CreateCreditCardAccountRequest("Nubank", "Mastercard", "1234", 20, 25, null, true),
            CancellationToken.None);

        var imported = await service.ImportCreditCardTransactionsAsync(
            account.Id,
            new ImportCreditCardTransactionsRequest(
                [
                    new ImportCreditCardTransactionItem(
                        "Supermercado",
                        "Mercado da esquina",
                        220.9m,
                        new DateOnly(2026, 7, 6),
                        "Compra do mês",
                        "Categoria importada",
                        "Espaço",
                        "Reforma",
                        "JSON",
                        "json-001",
                        null),
                    new ImportCreditCardTransactionItem(
                        "Reserva mensal",
                        null,
                        300m,
                        new DateOnly(2026, 7, 8),
                        null,
                        "Salário",
                        null,
                        null,
                        "JSON",
                        "json-002",
                        null)
                ]),
            CancellationToken.None);

        Assert.Equal(2, imported.TotalCount);
        Assert.Equal(520.9m, imported.TotalAmount);
        Assert.Equal(1, imported.CreatedCategoryCount);
        Assert.Equal(["Supermercado", "Reserva mensal"], imported.CreatedTransactions.Select(item => item.Title).ToArray());
        Assert.Equal("Categoria importada", imported.CreatedTransactions.First().CategoryName);
        Assert.Equal(fixture.ProjectId, imported.CreatedTransactions.First().ProjectId);
        Assert.Equal(fixture.CoreId, imported.CreatedTransactions.First().CoreId);
        Assert.Equal("Salário", imported.CreatedTransactions.Last().CategoryName);

        Assert.Equal(2, await context.CreditCardTransactions.CountAsync(item => item.CreditCardAccountId == account.Id));
        Assert.Single(await context.FinanceCategories.Where(item => item.SpaceId == fixture.SpaceId && item.Name == "Categoria importada").ToArrayAsync());
    }

    [Fact]
    public async Task Import_transactions_rolls_back_when_any_item_is_invalid()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var account = await service.CreateCreditCardAccountAsync(
            new CreateCreditCardAccountRequest("Nubank", "Mastercard", "1234", 20, 25, null, true),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.ImportCreditCardTransactionsAsync(
            account.Id,
            new ImportCreditCardTransactionsRequest(
                [
                    new ImportCreditCardTransactionItem(
                        "Compra válida",
                        null,
                        120m,
                        new DateOnly(2026, 7, 6),
                        null,
                        "Categoria nova",
                        null,
                        null,
                        "JSON",
                        "json-001",
                        null),
                    new ImportCreditCardTransactionItem(
                        "Compra inválida",
                        null,
                        50m,
                        new DateOnly(2026, 7, 7),
                        null,
                        null,
                        null,
                        "Projeto inexistente",
                        "JSON",
                        "json-002",
                        null)
                ]),
            CancellationToken.None));

        Assert.Equal("O projeto informado não pertence ao espaço ativo.", exception.Message);
        Assert.Empty(await context.CreditCardTransactions.Where(item => item.CreditCardAccountId == account.Id).ToArrayAsync());
        Assert.Empty(await context.FinanceCategories.Where(item => item.SpaceId == fixture.SpaceId && item.Name == "Categoria nova").ToArrayAsync());
    }

    [Fact]
    public async Task Superadmin_cannot_import_credit_card_transactions()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var ownerService = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var account = await ownerService.CreateCreditCardAccountAsync(
            new CreateCreditCardAccountRequest("Nubank", "Mastercard", "1234", 20, 25, null, true),
            CancellationToken.None);

        var superAdminService = CreateService(context, fixture.OwnerUserId, fixture.SpaceId, SystemRole.SuperAdmin);

        await Assert.ThrowsAsync<ForbiddenException>(() => superAdminService.ImportCreditCardTransactionsAsync(
            account.Id,
            new ImportCreditCardTransactionsRequest(
                [
                    new ImportCreditCardTransactionItem(
                        "Compra bloqueada",
                        null,
                        90m,
                        new DateOnly(2026, 7, 6),
                        null,
                        null,
                        null,
                        null,
                        "JSON",
                        "json-001",
                        null)
                ]),
            CancellationToken.None));
    }

    [Fact]
    public async Task Member_cannot_update_entry_created_by_another_person()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);

        var period = new FinancePeriod
        {
            SpaceId = fixture.SpaceId,
            Year = 2026,
            Month = 7
        };
        var entry = new FinanceEntry
        {
            SpaceId = fixture.SpaceId,
            FinancePeriod = period,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Salario",
            Amount = 1000m,
            Type = FinanceEntryType.Entrada,
            Verified = false,
            ReferenceDate = new DateOnly(2026, 7, 6),
            Origin = FinanceEntryOrigin.Manual
        };

        context.FinancePeriods.Add(period);
        context.FinanceEntries.Add(entry);
        await context.SaveChangesAsync();

        var service = CreateService(context, fixture.MemberUserId, fixture.SpaceId);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateEntryAsync(
            entry.Id,
            new UpdateFinanceEntryRequest(
                2026,
                7,
                "Salario ajustado",
                null,
                1100m,
                FinanceEntryType.Entrada,
                true,
                new DateOnly(2026, 7, 6),
                null,
                null,
                null,
                null),
            CancellationToken.None));
    }

    [Fact]
    public async Task List_categories_returns_defaults_in_expected_order()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        context.FinanceCategories.Add(new FinanceCategory
        {
            SpaceId = fixture.SpaceId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Name = "Pets",
            IsDefault = false,
            SortOrder = 20
        });
        context.FinanceCategories.Add(new FinanceCategory
        {
            SpaceId = fixture.SpaceId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Name = "Assinaturas",
            IsDefault = false,
            SortOrder = 19
        });
        await context.SaveChangesAsync();

        var categories = await service.ListCategoriesAsync(CancellationToken.None);

        Assert.Equal("Salário", categories.First().Name);
        Assert.Equal(FinanceCategoryCatalog.DefaultNames, categories.Take(FinanceCategoryCatalog.DefaultNames.Count).Select(item => item.Name).ToArray());
        Assert.Equal(["Assinaturas", "Pets"], categories.Skip(FinanceCategoryCatalog.DefaultNames.Count).Select(item => item.Name).ToArray());
    }

    [Fact]
    public async Task Default_category_cannot_be_edited_or_deleted()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var defaultCategory = await context.FinanceCategories.FirstAsync(item => item.SpaceId == fixture.SpaceId && item.IsDefault);

        var updateException = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateCategoryAsync(
            defaultCategory.Id,
            new UpdateFinanceCategoryRequest("Salário 2"),
            CancellationToken.None));

        var deleteException = await Assert.ThrowsAsync<ValidationException>(() => service.DeleteCategoryAsync(defaultCategory.Id, CancellationToken.None));

        Assert.Equal("Categorias padrão não podem ser editadas.", updateException.Message);
        Assert.Equal("Categorias padrão não podem ser excluídas.", deleteException.Message);
    }

    [Fact]
    public async Task Delete_category_unlinks_finance_records_and_member_cannot_manage_other_users_custom_category()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var ownerService = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var customCategory = new FinanceCategory
        {
            SpaceId = fixture.SpaceId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Name = "Pets",
            IsDefault = false,
            SortOrder = 20
        };
        var period = new FinancePeriod
        {
            SpaceId = fixture.SpaceId,
            Year = 2026,
            Month = 7
        };
        var recurringTemplate = new FinanceRecurringTemplate
        {
            SpaceId = fixture.SpaceId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Category = customCategory,
            Title = "Ração",
            DefaultAmount = 150m,
            Type = FinanceEntryType.Saida,
            Recurrence = FinanceRecurrence.Monthly,
            IsActive = true
        };
        var entry = new FinanceEntry
        {
            SpaceId = fixture.SpaceId,
            FinancePeriod = period,
            CreatedByMemberId = fixture.OwnerMemberId,
            Category = customCategory,
            Title = "Veterinário",
            Amount = 90m,
            Type = FinanceEntryType.Saida,
            Verified = false,
            ReferenceDate = new DateOnly(2026, 7, 6),
            Origin = FinanceEntryOrigin.Manual
        };
        var card = new CreditCardAccount
        {
            SpaceId = fixture.SpaceId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Name = "Nubank",
            ClosingDay = 20,
            DueDay = 25,
            IsActive = true
        };
        var transaction = new CreditCardTransaction
        {
            SpaceId = fixture.SpaceId,
            CreditCardAccount = card,
            CreatedByMemberId = fixture.OwnerMemberId,
            Category = customCategory,
            Title = "Pet shop",
            Amount = 120m,
            PurchasedOn = new DateOnly(2026, 7, 7)
        };

        context.FinanceCategories.Add(customCategory);
        context.FinancePeriods.Add(period);
        context.FinanceRecurringTemplates.Add(recurringTemplate);
        context.FinanceEntries.Add(entry);
        context.CreditCardAccounts.Add(card);
        context.CreditCardTransactions.Add(transaction);
        await context.SaveChangesAsync();

        var memberService = CreateService(context, fixture.MemberUserId, fixture.SpaceId);
        await Assert.ThrowsAsync<ForbiddenException>(() => memberService.DeleteCategoryAsync(customCategory.Id, CancellationToken.None));

        await ownerService.DeleteCategoryAsync(customCategory.Id, CancellationToken.None);

        Assert.Null((await context.FinanceEntries.FirstAsync()).CategoryId);
        Assert.Null((await context.FinanceRecurringTemplates.FirstAsync()).CategoryId);
        Assert.Null((await context.CreditCardTransactions.FirstAsync()).CategoryId);
    }

    [Fact]
    public async Task Create_entry_rejects_category_from_another_space()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var otherSpace = new Space { Name = "Outro espaço" };
        var otherOwner = new SpaceMember
        {
            Space = otherSpace,
            User = new AppUser
            {
                Email = $"other-owner-{Guid.NewGuid():N}@organiza.club",
                PasswordHash = "hash",
                DisplayName = "Other owner",
                SystemRole = SystemRole.User
            },
            Role = SpaceRole.Owner
        };
        var foreignCategory = new FinanceCategory
        {
            Space = otherSpace,
            CreatedByMember = otherOwner,
            Name = "Viagens",
            IsDefault = false,
            SortOrder = 20
        };
        context.Spaces.Add(otherSpace);
        context.SpaceMembers.Add(otherOwner);
        context.FinanceCategories.Add(foreignCategory);
        await context.SaveChangesAsync();

        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateEntryAsync(
            new CreateFinanceEntryRequest(
                2026,
                7,
                "Compra inválida",
                null,
                50m,
                FinanceEntryType.Saida,
                false,
                new DateOnly(2026, 7, 6),
                null,
                foreignCategory.Id,
                null,
                null),
            CancellationToken.None));

        Assert.Equal("A categoria informada não pertence ao espaço ativo.", exception.Message);
    }

    private static OrganizaClubDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrganizaClubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new OrganizaClubDbContext(options);
    }

    private static FinanceService CreateService(
        OrganizaClubDbContext context,
        Guid userId,
        Guid? spaceId,
        SystemRole systemRole = SystemRole.User)
    {
        return new FinanceService(
            context,
            new TestUserContext(userId, spaceId, systemRole));
    }

    private static async Task<Fixture> SeedFixtureAsync(OrganizaClubDbContext context)
    {
        var ownerUser = new AppUser
        {
            Email = $"finance-owner-{Guid.NewGuid():N}@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var memberUser = new AppUser
        {
            Email = $"finance-member-{Guid.NewGuid():N}@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Member",
            SystemRole = SystemRole.User
        };
        var space = new Space
        {
            Name = "Espaço Financeira"
        };
        var ownerMember = new SpaceMember
        {
            Space = space,
            User = ownerUser,
            Role = SpaceRole.Owner
        };
        var member = new SpaceMember
        {
            Space = space,
            User = memberUser,
            Role = SpaceRole.Member
        };

        context.Users.AddRange(ownerUser, memberUser);
        context.Spaces.Add(space);
        context.SpaceMembers.AddRange(ownerMember, member);
        await context.SaveChangesAsync();

        context.FinanceCategories.AddRange(FinanceCategoryCatalog.CreateDefaults(space.Id, ownerMember.Id));
        await context.SaveChangesAsync();

        var core = new Core
        {
            SpaceId = space.Id,
            CreatedByMemberId = ownerMember.Id,
            Name = "Espaço"
        };
        var otherCore = new Core
        {
            SpaceId = space.Id,
            CreatedByMemberId = ownerMember.Id,
            Name = "Projeto paralelo"
        };
        context.Cores.AddRange(core, otherCore);
        await context.SaveChangesAsync();

        var project = new Project
        {
            SpaceId = space.Id,
            CoreId = core.Id,
            CreatedByMemberId = ownerMember.Id,
            Name = "Reforma"
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        return new Fixture(
            space.Id,
            ownerUser.Id,
            ownerMember.Id,
            memberUser.Id,
            context.FinanceCategories.First(category => category.SpaceId == space.Id && category.Name == "Salário").Id,
            core.Id,
            otherCore.Id,
            project.Id);
    }

    private sealed record Fixture(
        Guid SpaceId,
        Guid OwnerUserId,
        Guid OwnerMemberId,
        Guid MemberUserId,
        Guid DefaultCategoryId,
        Guid CoreId,
        Guid OtherCoreId,
        Guid ProjectId);

    private sealed class TestUserContext(Guid userId, Guid? spaceId, SystemRole systemRole) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole { get; } = systemRole;
        public Guid? SpaceId { get; } = spaceId;
    }
}
