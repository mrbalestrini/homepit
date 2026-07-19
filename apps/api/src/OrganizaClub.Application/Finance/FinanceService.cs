using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Finance;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Finance;

public sealed class FinanceService(
    IOrganizaClubDbContext db,
    IUserContext userContext)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    public async Task<IReadOnlyCollection<FinancePeriodListItemDto>> ListPeriodsAsync(CancellationToken cancellationToken)
    {
        var spaceId = await ResolveSpaceIdAsync(cancellationToken);
        var periods = await db.FinancePeriods
            .AsNoTracking()
            .Include(period => period.Entries)
            .Where(period => period.SpaceId == spaceId)
            .OrderByDescending(period => period.Year)
            .ThenByDescending(period => period.Month)
            .ToArrayAsync(cancellationToken);

        return periods
            .Select(period =>
            {
                var totalIncome = period.Entries.Where(entry => entry.Type == FinanceEntryType.Entrada).Sum(entry => entry.Amount);
                var totalExpense = period.Entries.Where(entry => entry.Type == FinanceEntryType.Saida).Sum(entry => entry.Amount);
                return new FinancePeriodListItemDto(
                    period.Id,
                    period.Year,
                    period.Month,
                    totalIncome,
                    totalExpense,
                    totalIncome - totalExpense,
                    period.Entries.Count);
            })
            .ToArray();
    }

    public async Task<FinancePeriodDetailDto> GetPeriodAsync(int year, int month, CancellationToken cancellationToken)
    {
        ValidatePeriod(year, month);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);

        var period = await db.FinancePeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.SpaceId == currentMember.SpaceId && item.Year == year && item.Month == month, cancellationToken);

        var entries = await db.FinanceEntries
            .AsNoTracking()
            .Include(entry => entry.FinancePeriod)
            .Include(entry => entry.Category)
            .Include(entry => entry.Core)
            .Include(entry => entry.Project)
            .Where(entry => entry.SpaceId == currentMember.SpaceId && entry.FinancePeriod!.Year == year && entry.FinancePeriod.Month == month)
            .OrderBy(entry => entry.ReferenceDate)
            .ThenBy(entry => entry.Type)
            .ThenBy(entry => entry.Title)
            .ToArrayAsync(cancellationToken);

        var transactions = await db.CreditCardTransactions
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.Category)
            .Include(item => item.Core)
            .Include(item => item.Project)
            .Where(item => item.SpaceId == currentMember.SpaceId && item.PurchasedOn.Year == year && item.PurchasedOn.Month == month)
            .OrderByDescending(item => item.PurchasedOn)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);

        var statements = await db.CreditCardStatements
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.FinanceEntry)
            .Include(item => item.Transactions)
            .Where(item => item.SpaceId == currentMember.SpaceId && item.DueDate.Year == year && item.DueDate.Month == month)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.CreditCardAccount!.Name)
            .ToArrayAsync(cancellationToken);

        var entryDtos = entries.Select(entry => ToEntryDto(entry, currentMember)).ToArray();
        var transactionDtos = transactions.Select(transaction => ToTransactionDto(transaction, currentMember)).ToArray();
        var statementDtos = statements.Select(statement => ToStatementDto(statement, currentMember)).ToArray();

        var totalIncome = entries.Where(entry => entry.Type == FinanceEntryType.Entrada).Sum(entry => entry.Amount);
        var totalExpense = entries.Where(entry => entry.Type == FinanceEntryType.Saida).Sum(entry => entry.Amount);
        var analyticalExpenseTotal =
            entries.Where(entry => entry.Type == FinanceEntryType.Saida && entry.Origin != FinanceEntryOrigin.CreditCardStatement)
                .Sum(entry => entry.Amount) +
            transactions.Sum(transaction => transaction.Amount);

        return new FinancePeriodDetailDto(
            period?.Id,
            year,
            month,
            period is not null,
            new FinancePeriodSummaryDto(
                totalIncome,
                totalExpense,
                totalIncome - totalExpense,
                analyticalExpenseTotal,
                entries.Count(IsVerifiedEntry),
                entries.Count(entry => !IsVerifiedEntry(entry)),
                transactions.Length),
            entryDtos,
            transactionDtos,
            statementDtos);
    }

    public async Task<FinancePeriodDetailDto> GeneratePeriodAsync(
        int year,
        int month,
        GenerateFinancePeriodRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        ValidatePeriod(year, month);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var mode = NormalizeGenerateMode(request.Mode);
        var period = await GetOrCreatePeriodAsync(currentMember.SpaceId, year, month, cancellationToken);

        var templates = await db.FinanceRecurringTemplates
            .Where(item => item.SpaceId == currentMember.SpaceId && item.IsActive)
            .OrderBy(item => item.Title)
            .ToArrayAsync(cancellationToken);

        var existingTemplateIds = await db.FinanceEntries
            .Where(item => item.SpaceId == currentMember.SpaceId && item.FinancePeriodId == period.Id && item.RecurringTemplateId != null)
            .Select(item => item.RecurringTemplateId!.Value)
            .ToArrayAsync(cancellationToken);

        foreach (var template in templates.Where(template => AppliesToPeriod(template, month)))
        {
            if (mode == "missingOnly" && existingTemplateIds.Contains(template.Id))
            {
                continue;
            }

            var referenceDate = BuildReferenceDate(year, month, template.DayOfMonth);
            db.FinanceEntries.Add(new FinanceEntry
            {
                SpaceId = currentMember.SpaceId,
                FinancePeriodId = period.Id,
                CreatedByMemberId = currentMember.Id,
                RecurringTemplateId = template.Id,
                CategoryId = template.CategoryId,
                CoreId = template.CoreId,
                ProjectId = template.ProjectId,
                Title = template.Title,
                Notes = template.Notes,
                Amount = template.DefaultAmount,
                Type = template.Type,
                Verified = false,
                ReferenceDate = referenceDate,
                Origin = FinanceEntryOrigin.RecurringTemplate
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetPeriodAsync(year, month, cancellationToken);
    }

    public async Task<IReadOnlyCollection<FinanceCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var categories = await db.FinanceCategories
            .AsNoTracking()
            .Where(item => item.SpaceId == currentMember.SpaceId)
            .Select(item => new FinanceCategoryDto(
                item.Id,
                item.Name,
                item.IsDefault,
                item.SortOrder,
                item.CreatedByMemberId,
                item.Entries.Count +
                item.RecurringTemplates.Count +
                item.CreditCardTransactions.Count,
                !item.IsDefault && CanManageEntity(currentMember, item.CreatedByMemberId),
                !item.IsDefault && CanManageEntity(currentMember, item.CreatedByMemberId)))
            .ToArrayAsync(cancellationToken);

        return categories
            .OrderBy(item => item.IsDefault ? 0 : 1)
            .ThenBy(item => item.IsDefault ? item.SortOrder : int.MaxValue)
            .ThenBy(item => item.IsDefault ? string.Empty : item.Name)
            .ToArray();
    }

    public async Task<FinanceCategoryDto> CreateCategoryAsync(CreateFinanceCategoryRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var name = RequiredText(request.Name, "Informe o nome da categoria.");
        await EnsureFinanceCategoryNameAvailableAsync(currentMember.SpaceId, name, null, cancellationToken);

        var customSortOrder = await db.FinanceCategories
            .Where(item => item.SpaceId == currentMember.SpaceId && !item.IsDefault)
            .Select(item => (int?)item.SortOrder)
            .MaxAsync(cancellationToken) ?? (FinanceCategoryCatalog.DefaultNames.Count - 1);

        var category = new FinanceCategory
        {
            SpaceId = currentMember.SpaceId,
            CreatedByMemberId = currentMember.Id,
            Name = name,
            IsDefault = false,
            SortOrder = customSortOrder + 1
        };

        db.FinanceCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return new FinanceCategoryDto(category.Id, category.Name, false, category.SortOrder, category.CreatedByMemberId, 0, true, true);
    }

    public async Task<FinanceCategoryDto> UpdateCategoryAsync(
        Guid categoryId,
        UpdateFinanceCategoryRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = await db.FinanceCategories
            .Include(item => item.Entries)
            .Include(item => item.RecurringTemplates)
            .Include(item => item.CreditCardTransactions)
            .FirstOrDefaultAsync(item => item.Id == categoryId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Categoria não encontrada.");
        ApplyExpectedVersion(category, expectedUpdatedAt);

        if (category.IsDefault)
        {
            throw new ValidationException("Categorias padrão não podem ser editadas.");
        }

        EnsureCanManageEntity(currentMember, category.CreatedByMemberId, "Você não pode editar uma categoria criada por outra pessoa.");

        var name = RequiredText(request.Name, "Informe o nome da categoria.");
        await EnsureFinanceCategoryNameAvailableAsync(currentMember.SpaceId, name, category.Id, cancellationToken);

        category.Name = name;
        await db.SaveChangesAsync(cancellationToken);

        var usageCount = category.Entries.Count + category.RecurringTemplates.Count + category.CreditCardTransactions.Count;
        return new FinanceCategoryDto(category.Id, category.Name, false, category.SortOrder, category.CreatedByMemberId, usageCount, true, true);
    }

    public async Task DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = await db.FinanceCategories
            .FirstOrDefaultAsync(item => item.Id == categoryId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Categoria não encontrada.");
        ApplyExpectedVersion(category, expectedUpdatedAt);

        if (category.IsDefault)
        {
            throw new ValidationException("Categorias padrão não podem ser excluídas.");
        }

        EnsureCanManageEntity(currentMember, category.CreatedByMemberId, "Você não pode excluir uma categoria criada por outra pessoa.");

        foreach (var entry in await db.FinanceEntries.Where(item => item.CategoryId == category.Id).ToArrayAsync(cancellationToken))
        {
            entry.CategoryId = null;
            entry.Category = null;
        }

        foreach (var template in await db.FinanceRecurringTemplates.Where(item => item.CategoryId == category.Id).ToArrayAsync(cancellationToken))
        {
            template.CategoryId = null;
            template.Category = null;
        }

        foreach (var transaction in await db.CreditCardTransactions.Where(item => item.CategoryId == category.Id).ToArrayAsync(cancellationToken))
        {
            transaction.CategoryId = null;
            transaction.Category = null;
        }

        db.FinanceCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<FinanceRecurringTemplateDto>> ListRecurringTemplatesAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var templates = await db.FinanceRecurringTemplates
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.Core)
            .Include(item => item.Project)
            .Where(item => item.SpaceId == currentMember.SpaceId)
            .OrderBy(item => item.Recurrence)
            .ThenBy(item => item.Title)
            .ToArrayAsync(cancellationToken);

        return templates.Select(item => ToRecurringTemplateDto(item, currentMember)).ToArray();
    }

    public async Task<FinanceRecurringTemplateDto> CreateRecurringTemplateAsync(
        CreateFinanceRecurringTemplateRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var selection = await ResolveProjectCoreAsync(currentMember.SpaceId, request.CoreId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.SpaceId, request.CategoryId, cancellationToken);

        ValidateAmount(request.DefaultAmount, "O valor padrão da recorrência não pode ser negativo.");
        ValidateRecurrence(request.Recurrence, request.DayOfMonth, request.MonthOfYear);

        var template = new FinanceRecurringTemplate
        {
            SpaceId = currentMember.SpaceId,
            CreatedByMemberId = currentMember.Id,
            CoreId = selection.CoreId,
            ProjectId = selection.ProjectId,
            CategoryId = category?.Id,
            Title = RequiredText(request.Title, "Informe o título da recorrência."),
            Notes = NormalizeOptional(request.Notes),
            Type = request.Type,
            DefaultAmount = request.DefaultAmount,
            Recurrence = request.Recurrence,
            DayOfMonth = request.DayOfMonth,
            MonthOfYear = request.Recurrence == FinanceRecurrence.Annual ? request.MonthOfYear : null,
            IsActive = request.IsActive
        };

        db.FinanceRecurringTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);

        template.Category = category;
        template.Core = selection.Core;
        template.Project = selection.Project;
        return ToRecurringTemplateDto(template, currentMember);
    }

    public async Task<FinanceRecurringTemplateDto> UpdateRecurringTemplateAsync(
        Guid templateId,
        UpdateFinanceRecurringTemplateRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var template = await db.FinanceRecurringTemplates
            .Include(item => item.Category)
            .Include(item => item.Core)
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == templateId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Recorrência não encontrada.");
        ApplyExpectedVersion(template, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, template.CreatedByMemberId, "Você não pode editar uma recorrência criada por outra pessoa.");

        var selection = await ResolveProjectCoreAsync(currentMember.SpaceId, request.CoreId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.SpaceId, request.CategoryId, cancellationToken);
        ValidateAmount(request.DefaultAmount, "O valor padrão da recorrência não pode ser negativo.");
        ValidateRecurrence(request.Recurrence, request.DayOfMonth, request.MonthOfYear);

        template.CoreId = selection.CoreId;
        template.ProjectId = selection.ProjectId;
        template.CategoryId = category?.Id;
        template.Category = category;
        template.Title = RequiredText(request.Title, "Informe o título da recorrência.");
        template.Notes = NormalizeOptional(request.Notes);
        template.Type = request.Type;
        template.DefaultAmount = request.DefaultAmount;
        template.Recurrence = request.Recurrence;
        template.DayOfMonth = request.DayOfMonth;
        template.MonthOfYear = request.Recurrence == FinanceRecurrence.Annual ? request.MonthOfYear : null;
        template.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
        template.Core = selection.Core;
        template.Project = selection.Project;
        return ToRecurringTemplateDto(template, currentMember);
    }

    public async Task DeleteRecurringTemplateAsync(Guid templateId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var template = await db.FinanceRecurringTemplates
            .FirstOrDefaultAsync(item => item.Id == templateId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Recorrência não encontrada.");
        ApplyExpectedVersion(template, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, template.CreatedByMemberId, "Você não pode excluir uma recorrência criada por outra pessoa.");

        db.FinanceRecurringTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<FinanceEntryDto>> ListEntriesAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        if ((year.HasValue && !month.HasValue) || (!year.HasValue && month.HasValue))
        {
            throw new ValidationException("Informe ano e mês juntos para filtrar os lançamentos.");
        }

        if (year.HasValue && month.HasValue)
        {
            ValidatePeriod(year.Value, month.Value);
        }

        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var query = db.FinanceEntries
            .AsNoTracking()
            .Include(entry => entry.FinancePeriod)
            .Include(entry => entry.Category)
            .Include(entry => entry.Core)
            .Include(entry => entry.Project)
            .Where(entry => entry.SpaceId == currentMember.SpaceId);

        if (year.HasValue && month.HasValue)
        {
            query = query.Where(entry => entry.FinancePeriod!.Year == year.Value && entry.FinancePeriod.Month == month.Value);
        }

        var entries = await query
            .OrderByDescending(entry => entry.ReferenceDate)
            .ThenBy(entry => entry.Title)
            .ToArrayAsync(cancellationToken);

        return entries.Select(entry => ToEntryDto(entry, currentMember)).ToArray();
    }

    public async Task<FinanceEntryDto> CreateEntryAsync(CreateFinanceEntryRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        ValidatePeriod(request.Year, request.Month);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var period = await GetOrCreatePeriodAsync(currentMember.SpaceId, request.Year, request.Month, cancellationToken);
        var selection = await ResolveProjectCoreAsync(currentMember.SpaceId, request.CoreId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.SpaceId, request.CategoryId, cancellationToken);
        await EnsureRecurringTemplateAsync(currentMember.SpaceId, request.RecurringTemplateId, cancellationToken);

        ValidateAmount(request.Amount, "O valor do lançamento não pode ser negativo.");
        EnsureReferenceDateBelongsToPeriod(request.ReferenceDate, request.Year, request.Month);

        var entry = new FinanceEntry
        {
            SpaceId = currentMember.SpaceId,
            FinancePeriodId = period.Id,
            CreatedByMemberId = currentMember.Id,
            RecurringTemplateId = request.RecurringTemplateId,
            CoreId = selection.CoreId,
            ProjectId = selection.ProjectId,
            CategoryId = category?.Id,
            Title = RequiredText(request.Title, "Informe o título do lançamento."),
            Notes = NormalizeOptional(request.Notes),
            Amount = request.Amount,
            Type = request.Type,
            Verified = request.Verified,
            ReferenceDate = request.ReferenceDate,
            Origin = request.RecurringTemplateId.HasValue ? FinanceEntryOrigin.RecurringTemplate : FinanceEntryOrigin.Manual
        };

        db.FinanceEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        entry.Category = category;
        entry.FinancePeriod = period;
        entry.Core = selection.Core;
        entry.Project = selection.Project;
        return ToEntryDto(entry, currentMember);
    }

    public async Task<FinanceEntryDto> UpdateEntryAsync(
        Guid entryId,
        UpdateFinanceEntryRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        ValidatePeriod(request.Year, request.Month);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var entry = await db.FinanceEntries
            .Include(item => item.FinancePeriod)
            .Include(item => item.Category)
            .Include(item => item.Core)
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == entryId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Lançamento não encontrado.");
        ApplyExpectedVersion(entry, expectedUpdatedAt);

        if (entry.Origin == FinanceEntryOrigin.CreditCardStatement)
        {
            throw new ValidationException("A fatura consolidada deve ser alterada pelo cartão de crédito.");
        }

        EnsureCanManageEntity(currentMember, entry.CreatedByMemberId, "Você não pode editar um lançamento criado por outra pessoa.");

        var period = await GetOrCreatePeriodAsync(currentMember.SpaceId, request.Year, request.Month, cancellationToken);
        var selection = await ResolveProjectCoreAsync(currentMember.SpaceId, request.CoreId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.SpaceId, request.CategoryId, cancellationToken);
        await EnsureRecurringTemplateAsync(currentMember.SpaceId, request.RecurringTemplateId, cancellationToken);
        ValidateAmount(request.Amount, "O valor do lançamento não pode ser negativo.");
        EnsureReferenceDateBelongsToPeriod(request.ReferenceDate, request.Year, request.Month);

        entry.FinancePeriodId = period.Id;
        entry.FinancePeriod = period;
        entry.CoreId = selection.CoreId;
        entry.ProjectId = selection.ProjectId;
        entry.CategoryId = category?.Id;
        entry.Core = selection.Core;
        entry.Project = selection.Project;
        entry.Category = category;
        entry.RecurringTemplateId = request.RecurringTemplateId;
        entry.Title = RequiredText(request.Title, "Informe o título do lançamento.");
        entry.Notes = NormalizeOptional(request.Notes);
        entry.Amount = request.Amount;
        entry.Type = request.Type;
        entry.Verified = request.Verified;
        entry.ReferenceDate = request.ReferenceDate;
        entry.Origin = request.RecurringTemplateId.HasValue ? FinanceEntryOrigin.RecurringTemplate : FinanceEntryOrigin.Manual;

        await db.SaveChangesAsync(cancellationToken);
        return ToEntryDto(entry, currentMember);
    }

    public async Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var entry = await db.FinanceEntries
            .FirstOrDefaultAsync(item => item.Id == entryId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Lançamento não encontrado.");
        ApplyExpectedVersion(entry, expectedUpdatedAt);

        if (entry.Origin == FinanceEntryOrigin.CreditCardStatement)
        {
            throw new ValidationException("A fatura consolidada deve ser removida pelo cartão de crédito.");
        }

        EnsureCanManageEntity(currentMember, entry.CreatedByMemberId, "Você não pode excluir um lançamento criado por outra pessoa.");
        db.FinanceEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetDto>> ListAssetsAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var assets = await db.Assets
            .AsNoTracking()
            .Include(item => item.PropertyDetails)
            .Include(item => item.VehicleDetails)
            .Where(item => item.SpaceId == currentMember.SpaceId)
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Title)
            .ToArrayAsync(cancellationToken);

        return assets.Select(item => ToAssetDto(item, currentMember)).ToArray();
    }

    public async Task<AssetDto> CreateAssetAsync(CreateAssetRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        ValidateAmount(request.CurrentValue, "O valor atual do bem não pode ser negativo.");
        ValidateAmount(request.RemainingDebt, "A dívida restante do bem não pode ser negativa.");
        ValidateAssetDetails(request.Type, request.PropertyDetails, request.VehicleDetails);

        var asset = new Asset
        {
            SpaceId = currentMember.SpaceId,
            CreatedByMemberId = currentMember.Id,
            Title = RequiredText(request.Title, "Informe o título do bem."),
            Type = request.Type,
            CurrentValue = request.CurrentValue,
            RemainingDebt = request.RemainingDebt,
            IsPaidOff = request.IsPaidOff,
            Notes = NormalizeOptional(request.Notes)
        };

        ApplyAssetDetails(asset, request.Type, request.PropertyDetails, request.VehicleDetails);

        db.Assets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        return ToAssetDto(asset, currentMember);
    }

    public async Task<AssetDto> UpdateAssetAsync(Guid assetId, UpdateAssetRequest request, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .Include(item => item.PropertyDetails)
            .Include(item => item.VehicleDetails)
            .FirstOrDefaultAsync(item => item.Id == assetId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");
        ApplyExpectedVersion(asset, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, asset.CreatedByMemberId, "Você não pode editar um bem criado por outra pessoa.");

        ValidateAmount(request.CurrentValue, "O valor atual do bem não pode ser negativo.");
        ValidateAmount(request.RemainingDebt, "A dívida restante do bem não pode ser negativa.");
        ValidateAssetDetails(request.Type, request.PropertyDetails, request.VehicleDetails);

        asset.Title = RequiredText(request.Title, "Informe o título do bem.");
        asset.Type = request.Type;
        asset.CurrentValue = request.CurrentValue;
        asset.RemainingDebt = request.RemainingDebt;
        asset.IsPaidOff = request.IsPaidOff;
        asset.Notes = NormalizeOptional(request.Notes);

        ApplyAssetDetails(asset, request.Type, request.PropertyDetails, request.VehicleDetails);

        await db.SaveChangesAsync(cancellationToken);
        return ToAssetDto(asset, currentMember);
    }

    public async Task DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .FirstOrDefaultAsync(item => item.Id == assetId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");
        ApplyExpectedVersion(asset, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, asset.CreatedByMemberId, "Você não pode excluir um bem criado por outra pessoa.");
        db.Assets.Remove(asset);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetValuationDto>> ListAssetValuationsAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == assetId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");

        var valuations = await db.AssetValuations
            .AsNoTracking()
            .Where(item => item.AssetId == asset.Id)
            .OrderByDescending(item => item.ReferenceYear)
            .ThenBy(item => item.Label)
            .ToArrayAsync(cancellationToken);

        return valuations.Select(item => ToAssetValuationDto(item, CanManageEntity(currentMember, asset.CreatedByMemberId))).ToArray();
    }

    public async Task<AssetValuationDto> CreateAssetValuationAsync(
        Guid assetId,
        CreateAssetValuationRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .FirstOrDefaultAsync(item => item.Id == assetId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");

        EnsureCanManageEntity(currentMember, asset.CreatedByMemberId, "Você não pode registrar uma avaliação em um bem criado por outra pessoa.");
        ValidateReferenceYear(request.ReferenceYear);
        ValidatePositiveAmount(request.Amount, "O valor de referência deve ser maior que zero.");

        var valuation = new AssetValuation
        {
            AssetId = asset.Id,
            ReferenceYear = request.ReferenceYear,
            Label = RequiredText(request.Label, "Informe o rótulo da referência anual."),
            Amount = request.Amount,
            Notes = NormalizeOptional(request.Notes)
        };

        db.AssetValuations.Add(valuation);
        await db.SaveChangesAsync(cancellationToken);
        return ToAssetValuationDto(valuation, true);
    }

    public async Task<AssetValuationDto> UpdateAssetValuationAsync(
        Guid assetId,
        Guid valuationId,
        UpdateAssetValuationRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == assetId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");

        EnsureCanManageEntity(currentMember, asset.CreatedByMemberId, "Você não pode editar uma avaliação de um bem criado por outra pessoa.");

        var valuation = await db.AssetValuations
            .FirstOrDefaultAsync(item => item.Id == valuationId && item.AssetId == assetId, cancellationToken)
            ?? throw new NotFoundException("Avaliação não encontrada.");
        ApplyExpectedVersion(valuation, expectedUpdatedAt);

        ValidateReferenceYear(request.ReferenceYear);
        ValidatePositiveAmount(request.Amount, "O valor de referência deve ser maior que zero.");

        valuation.ReferenceYear = request.ReferenceYear;
        valuation.Label = RequiredText(request.Label, "Informe o rótulo da referência anual.");
        valuation.Amount = request.Amount;
        valuation.Notes = NormalizeOptional(request.Notes);

        await db.SaveChangesAsync(cancellationToken);
        return ToAssetValuationDto(valuation, true);
    }

    public async Task DeleteAssetValuationAsync(Guid assetId, Guid valuationId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == assetId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");

        EnsureCanManageEntity(currentMember, asset.CreatedByMemberId, "Você não pode excluir uma avaliação de um bem criado por outra pessoa.");

        var valuation = await db.AssetValuations
            .FirstOrDefaultAsync(item => item.Id == valuationId && item.AssetId == assetId, cancellationToken)
            ?? throw new NotFoundException("Avaliação não encontrada.");
        ApplyExpectedVersion(valuation, expectedUpdatedAt);

        db.AssetValuations.Remove(valuation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CreditCardAccountDto>> ListCreditCardAccountsAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var accounts = await db.CreditCardAccounts
            .AsNoTracking()
            .Include(account => account.Transactions)
            .Where(account => account.SpaceId == currentMember.SpaceId)
            .OrderByDescending(account => account.IsActive)
            .ThenBy(account => account.Name)
            .ToArrayAsync(cancellationToken);

        return accounts.Select(account => ToCreditCardAccountDto(account, currentMember)).ToArray();
    }

    public async Task<CreditCardAccountDto> CreateCreditCardAccountAsync(
        CreateCreditCardAccountRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        ValidateCardDay(request.ClosingDay, "O dia de fechamento do cartão deve estar entre 1 e 31.");
        ValidateCardDay(request.DueDay, "O dia de vencimento do cartão deve estar entre 1 e 31.");

        var account = new CreditCardAccount
        {
            SpaceId = currentMember.SpaceId,
            CreatedByMemberId = currentMember.Id,
            Name = RequiredText(request.Name, "Informe o nome do cartão."),
            Brand = NormalizeOptional(request.Brand),
            LastFourDigits = NormalizeLastFourDigits(request.LastFourDigits),
            ClosingDay = request.ClosingDay,
            DueDay = request.DueDay,
            Notes = NormalizeOptional(request.Notes),
            IsActive = request.IsActive
        };

        db.CreditCardAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return ToCreditCardAccountDto(account, currentMember);
    }

    public async Task<CreditCardAccountDto> UpdateCreditCardAccountAsync(
        Guid accountId,
        UpdateCreditCardAccountRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var account = await db.CreditCardAccounts
            .Include(item => item.Transactions)
            .FirstOrDefaultAsync(item => item.Id == accountId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Cartão não encontrado.");
        ApplyExpectedVersion(account, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, account.CreatedByMemberId, "Você não pode editar um cartão criado por outra pessoa.");
        ValidateCardDay(request.ClosingDay, "O dia de fechamento do cartão deve estar entre 1 e 31.");
        ValidateCardDay(request.DueDay, "O dia de vencimento do cartão deve estar entre 1 e 31.");

        account.Name = RequiredText(request.Name, "Informe o nome do cartão.");
        account.Brand = NormalizeOptional(request.Brand);
        account.LastFourDigits = NormalizeLastFourDigits(request.LastFourDigits);
        account.ClosingDay = request.ClosingDay;
        account.DueDay = request.DueDay;
        account.Notes = NormalizeOptional(request.Notes);
        account.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
        return ToCreditCardAccountDto(account, currentMember);
    }

    public async Task DeleteCreditCardAccountAsync(Guid accountId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var account = await db.CreditCardAccounts
            .Include(item => item.Statements)
            .FirstOrDefaultAsync(item => item.Id == accountId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Cartão não encontrado.");
        ApplyExpectedVersion(account, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, account.CreatedByMemberId, "Você não pode excluir um cartão criado por outra pessoa.");

        var statementIds = account.Statements.Select(statement => statement.Id).ToArray();
        var generatedEntries = await db.FinanceEntries
            .Where(entry => entry.CreditCardStatementId != null && statementIds.Contains(entry.CreditCardStatementId.Value))
            .ToArrayAsync(cancellationToken);

        db.FinanceEntries.RemoveRange(generatedEntries);
        db.CreditCardAccounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CreditCardTransactionDto>> ListCreditCardTransactionsAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await EnsureCreditCardAccountAsync(currentMember.SpaceId, accountId, cancellationToken);

        var transactions = await db.CreditCardTransactions
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.Category)
            .Include(item => item.Core)
            .Include(item => item.Project)
            .Where(item => item.SpaceId == currentMember.SpaceId && item.CreditCardAccountId == accountId)
            .OrderByDescending(item => item.PurchasedOn)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);

        return transactions.Select(item => ToTransactionDto(item, currentMember)).ToArray();
    }

    public async Task<CreditCardTransactionDto> CreateCreditCardTransactionAsync(
        Guid accountId,
        CreateCreditCardTransactionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await EnsureCreditCardAccountAsync(currentMember.SpaceId, accountId, cancellationToken);
        var selection = await ResolveProjectCoreAsync(currentMember.SpaceId, request.CoreId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.SpaceId, request.CategoryId, cancellationToken);
        ValidatePositiveAmount(request.Amount, "O valor da compra no cartão deve ser maior que zero.");

        var transaction = new CreditCardTransaction
        {
            SpaceId = currentMember.SpaceId,
            CreditCardAccountId = accountId,
            CreatedByMemberId = currentMember.Id,
            CoreId = selection.CoreId,
            ProjectId = selection.ProjectId,
            CategoryId = category?.Id,
            Title = RequiredText(request.Title, "Informe o título da compra no cartão."),
            Merchant = NormalizeOptional(request.Merchant),
            Amount = request.Amount,
            PurchasedOn = request.PurchasedOn,
            Notes = NormalizeOptional(request.Notes),
            ExternalSource = NormalizeOptional(request.ExternalSource),
            ExternalReference = NormalizeOptional(request.ExternalReference),
            ImportedAt = request.ImportedAt
        };

        db.CreditCardTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        transaction.CreditCardAccount = await GetTrackedCreditCardAccountAsync(accountId, cancellationToken);
        transaction.Category = category;
        transaction.Core = selection.Core;
        transaction.Project = selection.Project;
        return ToTransactionDto(transaction, currentMember);
    }

    public async Task<ImportCreditCardTransactionsResponse> ImportCreditCardTransactionsAsync(
        Guid accountId,
        ImportCreditCardTransactionsRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await EnsureCreditCardAccountAsync(currentMember.SpaceId, accountId, cancellationToken);

        if (request.Transactions is null || request.Transactions.Count == 0)
        {
            throw new ValidationException("Envie ao menos uma compra para importar.");
        }

        var existingCategories = await db.FinanceCategories
            .AsNoTracking()
            .Where(item => item.SpaceId == currentMember.SpaceId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        var categoriesByNormalizedName = existingCategories
            .GroupBy(item => NormalizeNameKey(item.Name))
            .ToDictionary(group => group.Key, group => group.First());
        var nextCategorySortOrder = existingCategories.Length == 0 ? 1 : existingCategories.Max(item => item.SortOrder) + 1;
        var createdCategoryCount = 0;
        var createdTransactions = new List<CreditCardTransaction>(request.Transactions.Count);

        foreach (var item in request.Transactions)
        {
            var selection = await ResolveProjectCoreByNameAsync(currentMember.SpaceId, item.CoreName, item.ProjectName, cancellationToken);
            var category = ResolveOrCreateFinanceCategoryByName(
                currentMember.SpaceId,
                currentMember.Id,
                categoriesByNormalizedName,
                ref nextCategorySortOrder,
                ref createdCategoryCount,
                item.CategoryName);
            ValidatePositiveAmount(item.Amount, "O valor da compra no cartão deve ser maior que zero.");

            var transaction = new CreditCardTransaction
            {
                SpaceId = currentMember.SpaceId,
                CreditCardAccountId = accountId,
                CreatedByMemberId = currentMember.Id,
                CoreId = selection.CoreId,
                ProjectId = selection.ProjectId,
                CategoryId = category?.Id,
                Title = RequiredText(item.Title, "Informe o título da compra no cartão."),
                Merchant = NormalizeOptional(item.Merchant),
                Amount = item.Amount,
                PurchasedOn = item.PurchasedOn,
                Notes = NormalizeOptional(item.Notes),
                ExternalSource = NormalizeOptional(item.ExternalSource),
                ExternalReference = NormalizeOptional(item.ExternalReference),
                ImportedAt = item.ImportedAt
            };

            db.CreditCardTransactions.Add(transaction);
            createdTransactions.Add(transaction);
        }

        await db.SaveChangesAsync(cancellationToken);

        var createdTransactionIds = createdTransactions.Select(item => item.Id).ToArray();
        var persistedTransactions = await db.CreditCardTransactions
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.Category)
            .Include(item => item.Core)
            .Include(item => item.Project)
            .Where(item => createdTransactionIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        var persistedById = persistedTransactions.ToDictionary(item => item.Id);
        var orderedDtos = createdTransactionIds
            .Select(id => persistedById[id])
            .Select(item => ToTransactionDto(item, currentMember))
            .ToArray();

        return new ImportCreditCardTransactionsResponse(
            orderedDtos.Length,
            orderedDtos.Sum(item => item.Amount),
            createdCategoryCount,
            orderedDtos);
    }

    public async Task<CreditCardTransactionDto> UpdateCreditCardTransactionAsync(
        Guid accountId,
        Guid transactionId,
        UpdateCreditCardTransactionRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var transaction = await db.CreditCardTransactions
            .Include(item => item.CreditCardAccount)
            .Include(item => item.Category)
            .Include(item => item.Core)
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item =>
                item.Id == transactionId &&
                item.CreditCardAccountId == accountId &&
                item.SpaceId == currentMember.SpaceId,
                cancellationToken)
            ?? throw new NotFoundException("Compra no cartão não encontrada.");
        ApplyExpectedVersion(transaction, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, transaction.CreatedByMemberId, "Você não pode editar uma compra no cartão criada por outra pessoa.");

        var selection = await ResolveProjectCoreAsync(currentMember.SpaceId, request.CoreId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.SpaceId, request.CategoryId, cancellationToken);
        ValidatePositiveAmount(request.Amount, "O valor da compra no cartão deve ser maior que zero.");

        transaction.CoreId = selection.CoreId;
        transaction.ProjectId = selection.ProjectId;
        transaction.CategoryId = category?.Id;
        transaction.Core = selection.Core;
        transaction.Project = selection.Project;
        transaction.Category = category;
        transaction.Title = RequiredText(request.Title, "Informe o título da compra no cartão.");
        transaction.Merchant = NormalizeOptional(request.Merchant);
        transaction.Amount = request.Amount;
        transaction.PurchasedOn = request.PurchasedOn;
        transaction.Notes = NormalizeOptional(request.Notes);
        transaction.ExternalSource = NormalizeOptional(request.ExternalSource);
        transaction.ExternalReference = NormalizeOptional(request.ExternalReference);
        transaction.ImportedAt = request.ImportedAt;

        await db.SaveChangesAsync(cancellationToken);

        if (transaction.CreditCardStatementId.HasValue)
        {
            await RecalculateStatementAsync(transaction.CreditCardStatementId.Value, cancellationToken);
        }

        return ToTransactionDto(transaction, currentMember);
    }

    public async Task DeleteCreditCardTransactionAsync(Guid accountId, Guid transactionId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var transaction = await db.CreditCardTransactions
            .FirstOrDefaultAsync(item =>
                item.Id == transactionId &&
                item.CreditCardAccountId == accountId &&
                item.SpaceId == currentMember.SpaceId,
                cancellationToken)
            ?? throw new NotFoundException("Compra no cartão não encontrada.");
        ApplyExpectedVersion(transaction, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, transaction.CreatedByMemberId, "Você não pode excluir uma compra no cartão criada por outra pessoa.");

        var statementId = transaction.CreditCardStatementId;
        db.CreditCardTransactions.Remove(transaction);
        await db.SaveChangesAsync(cancellationToken);

        if (statementId.HasValue)
        {
            await RecalculateStatementAsync(statementId.Value, cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<CreditCardStatementDto>> ListCreditCardStatementsAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await EnsureCreditCardAccountAsync(currentMember.SpaceId, accountId, cancellationToken);

        var statements = await db.CreditCardStatements
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.FinanceEntry)
            .Include(item => item.Transactions)
            .Where(item => item.SpaceId == currentMember.SpaceId && item.CreditCardAccountId == accountId)
            .OrderByDescending(item => item.DueDate)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);

        return statements.Select(item => ToStatementDto(item, currentMember)).ToArray();
    }

    public async Task<CreditCardStatementDto> CreateCreditCardStatementAsync(
        Guid accountId,
        CreateCreditCardStatementRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var account = await db.CreditCardAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == accountId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Cartão não encontrado.");

        ValidateStatementDates(request.ClosingDate, request.DueDate);

        var statement = new CreditCardStatement
        {
            SpaceId = currentMember.SpaceId,
            CreditCardAccountId = account.Id,
            CreatedByMemberId = currentMember.Id,
            ClosingDate = request.ClosingDate,
            DueDate = request.DueDate,
            Notes = NormalizeOptional(request.Notes),
            ExternalSource = NormalizeOptional(request.ExternalSource),
            ExternalReference = NormalizeOptional(request.ExternalReference),
            ImportedAt = request.ImportedAt
        };

        db.CreditCardStatements.Add(statement);
        await db.SaveChangesAsync(cancellationToken);

        await AssignTransactionsToStatementAsync(currentMember.SpaceId, account.Id, statement.Id, request.TransactionIds, cancellationToken);
        await RecalculateStatementAsync(statement.Id, cancellationToken);

        return await GetStatementAsync(statement.Id, currentMember, cancellationToken);
    }

    public async Task<CreditCardStatementDto> UpdateCreditCardStatementAsync(
        Guid accountId,
        Guid statementId,
        UpdateCreditCardStatementRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var statement = await db.CreditCardStatements
            .Include(item => item.CreditCardAccount)
            .FirstOrDefaultAsync(item =>
                item.Id == statementId &&
                item.CreditCardAccountId == accountId &&
                item.SpaceId == currentMember.SpaceId,
                cancellationToken)
            ?? throw new NotFoundException("Fatura não encontrada.");
        ApplyExpectedVersion(statement, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, statement.CreatedByMemberId, "Você não pode editar uma fatura criada por outra pessoa.");
        ValidateStatementDates(request.ClosingDate, request.DueDate);

        statement.ClosingDate = request.ClosingDate;
        statement.DueDate = request.DueDate;
        statement.Notes = NormalizeOptional(request.Notes);
        statement.ExternalSource = NormalizeOptional(request.ExternalSource);
        statement.ExternalReference = NormalizeOptional(request.ExternalReference);
        statement.ImportedAt = request.ImportedAt;

        await db.SaveChangesAsync(cancellationToken);
        await AssignTransactionsToStatementAsync(currentMember.SpaceId, accountId, statement.Id, request.TransactionIds, cancellationToken);
        await RecalculateStatementAsync(statement.Id, cancellationToken);

        return await GetStatementAsync(statement.Id, currentMember, cancellationToken);
    }

    public async Task DeleteCreditCardStatementAsync(Guid accountId, Guid statementId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var statement = await db.CreditCardStatements
            .Include(item => item.Transactions)
            .FirstOrDefaultAsync(item =>
                item.Id == statementId &&
                item.CreditCardAccountId == accountId &&
                item.SpaceId == currentMember.SpaceId,
                cancellationToken)
            ?? throw new NotFoundException("Fatura não encontrada.");
        ApplyExpectedVersion(statement, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, statement.CreatedByMemberId, "Você não pode excluir uma fatura criada por outra pessoa.");

        foreach (var transaction in statement.Transactions)
        {
            transaction.CreditCardStatementId = null;
        }

        var generatedEntry = await db.FinanceEntries
            .FirstOrDefaultAsync(entry => entry.CreditCardStatementId == statement.Id, cancellationToken);
        if (generatedEntry is not null)
        {
            db.FinanceEntries.Remove(generatedEntry);
        }

        db.CreditCardStatements.Remove(statement);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CreditCardStatementDto> GetStatementAsync(
        Guid statementId,
        SpaceMember currentMember,
        CancellationToken cancellationToken)
    {
        var statement = await db.CreditCardStatements
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.FinanceEntry)
            .Include(item => item.Transactions)
            .FirstOrDefaultAsync(item => item.Id == statementId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Fatura não encontrada.");

        return ToStatementDto(statement, currentMember);
    }

    private async Task AssignTransactionsToStatementAsync(
        Guid spaceId,
        Guid accountId,
        Guid statementId,
        Guid[] transactionIds,
        CancellationToken cancellationToken)
    {
        var normalizedIds = transactionIds.Distinct().ToArray();
        var existingAssigned = await db.CreditCardTransactions
            .Where(item => item.CreditCardStatementId == statementId)
            .ToArrayAsync(cancellationToken);

        foreach (var transaction in existingAssigned.Where(item => !normalizedIds.Contains(item.Id)))
        {
            transaction.CreditCardStatementId = null;
        }

        if (normalizedIds.Length == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var selectedTransactions = await db.CreditCardTransactions
            .Where(item =>
                item.SpaceId == spaceId &&
                item.CreditCardAccountId == accountId &&
                normalizedIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);

        if (selectedTransactions.Length != normalizedIds.Length)
        {
            throw new ValidationException("Selecione apenas compras do cartão atual para compor a fatura.");
        }

        if (selectedTransactions.Any(item => item.CreditCardStatementId.HasValue && item.CreditCardStatementId != statementId))
        {
            throw new ValidationException("Uma compra selecionada já pertence a outra fatura.");
        }

        foreach (var transaction in selectedTransactions)
        {
            transaction.CreditCardStatementId = statementId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecalculateStatementAsync(Guid statementId, CancellationToken cancellationToken)
    {
        var statement = await db.CreditCardStatements
            .Include(item => item.CreditCardAccount)
            .Include(item => item.Transactions)
            .FirstOrDefaultAsync(item => item.Id == statementId, cancellationToken)
            ?? throw new NotFoundException("Fatura não encontrada.");

        statement.TotalAmount = statement.Transactions.Sum(item => item.Amount);
        await db.SaveChangesAsync(cancellationToken);

        var period = await GetOrCreatePeriodAsync(statement.SpaceId, statement.DueDate.Year, statement.DueDate.Month, cancellationToken);
        var generatedEntry = await db.FinanceEntries
            .FirstOrDefaultAsync(entry => entry.CreditCardStatementId == statement.Id, cancellationToken);

        var entryTitle = $"Fatura {statement.CreditCardAccount!.Name} - {statement.DueDate:MM/yyyy}";
        if (generatedEntry is null)
        {
            generatedEntry = new FinanceEntry
            {
                SpaceId = statement.SpaceId,
                FinancePeriodId = period.Id,
                CreatedByMemberId = statement.CreatedByMemberId,
                CreditCardStatementId = statement.Id,
                CategoryId = null,
                Title = entryTitle,
                Notes = statement.Notes,
                Amount = statement.TotalAmount,
                Type = FinanceEntryType.Saida,
                Verified = false,
                ReferenceDate = statement.DueDate,
                Origin = FinanceEntryOrigin.CreditCardStatement
            };

            db.FinanceEntries.Add(generatedEntry);
        }
        else
        {
            generatedEntry.FinancePeriodId = period.Id;
            generatedEntry.CreatedByMemberId = statement.CreatedByMemberId;
            generatedEntry.CategoryId = null;
            generatedEntry.Title = entryTitle;
            generatedEntry.Notes = statement.Notes;
            generatedEntry.Amount = statement.TotalAmount;
            generatedEntry.Type = FinanceEntryType.Saida;
            generatedEntry.Verified = true;
            generatedEntry.ReferenceDate = statement.DueDate;
            generatedEntry.Origin = FinanceEntryOrigin.CreditCardStatement;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCreditCardAccountAsync(Guid spaceId, Guid accountId, CancellationToken cancellationToken)
    {
        var exists = await db.CreditCardAccounts
            .AnyAsync(item => item.Id == accountId && item.SpaceId == spaceId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Cartão não encontrado.");
        }
    }

    private async Task<CreditCardAccount> GetTrackedCreditCardAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var trackedAccount = db.CreditCardAccounts.Local.FirstOrDefault(item => item.Id == accountId);
        if (trackedAccount is not null)
        {
            return trackedAccount;
        }

        return await db.CreditCardAccounts.FirstAsync(item => item.Id == accountId, cancellationToken);
    }

    private async Task EnsureRecurringTemplateAsync(Guid spaceId, Guid? recurringTemplateId, CancellationToken cancellationToken)
    {
        if (!recurringTemplateId.HasValue)
        {
            return;
        }

        var exists = await db.FinanceRecurringTemplates
            .AnyAsync(item => item.Id == recurringTemplateId.Value && item.SpaceId == spaceId, cancellationToken);

        if (!exists)
        {
            throw new ValidationException("A recorrência informada não pertence a este espaço.");
        }
    }

    private async Task<FinanceCategory?> ResolveFinanceCategoryAsync(Guid spaceId, Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        return await db.FinanceCategories
            .FirstOrDefaultAsync(item => item.Id == categoryId.Value && item.SpaceId == spaceId, cancellationToken)
            ?? throw new ValidationException("A categoria informada não pertence ao espaço ativo.");
    }

    private FinanceCategory? ResolveOrCreateFinanceCategoryByName(
        Guid spaceId,
        Guid? createdByMemberId,
        IDictionary<string, FinanceCategory> categoriesByNormalizedName,
        ref int nextCategorySortOrder,
        ref int createdCategoryCount,
        string? categoryName)
    {
        var normalizedName = NormalizeOptional(categoryName);
        if (normalizedName is null)
        {
            return null;
        }

        var normalizedKey = NormalizeNameKey(normalizedName);
        if (categoriesByNormalizedName.TryGetValue(normalizedKey, out var existingCategory))
        {
            return existingCategory;
        }

        var category = new FinanceCategory
        {
            SpaceId = spaceId,
            CreatedByMemberId = createdByMemberId,
            Name = normalizedName,
            IsDefault = false,
            SortOrder = nextCategorySortOrder++
        };

        db.FinanceCategories.Add(category);
        categoriesByNormalizedName[normalizedKey] = category;
        createdCategoryCount++;
        return category;
    }

    private async Task EnsureFinanceCategoryNameAvailableAsync(
        Guid spaceId,
        string name,
        Guid? currentCategoryId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.ToUpperInvariant();
        var exists = await db.FinanceCategories
            .AnyAsync(
                item =>
                    item.SpaceId == spaceId &&
                    item.Id != currentCategoryId &&
                    item.Name.ToUpper() == normalizedName,
                cancellationToken);

        if (exists)
        {
            throw new ValidationException("Já existe uma categoria com esse nome neste espaço.");
        }
    }

    private async Task<FinancePeriod> GetOrCreatePeriodAsync(Guid spaceId, int year, int month, CancellationToken cancellationToken)
    {
        var period = await db.FinancePeriods
            .FirstOrDefaultAsync(item => item.SpaceId == spaceId && item.Year == year && item.Month == month, cancellationToken);

        if (period is not null)
        {
            return period;
        }

        period = new FinancePeriod
        {
            SpaceId = spaceId,
            Year = year,
            Month = month
        };

        db.FinancePeriods.Add(period);
        await db.SaveChangesAsync(cancellationToken);
        return period;
    }

    private static bool AppliesToPeriod(FinanceRecurringTemplate template, int month)
    {
        return template.Recurrence switch
        {
            FinanceRecurrence.Monthly => true,
            FinanceRecurrence.Annual => template.MonthOfYear == month,
            _ => false
        };
    }

    private static DateOnly BuildReferenceDate(int year, int month, int? dayOfMonth)
    {
        var targetDay = Math.Clamp(dayOfMonth ?? 1, 1, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, targetDay);
    }

    private async Task<ProjectCoreSelection> ResolveProjectCoreAsync(
        Guid spaceId,
        Guid? coreId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        Core? core = null;
        Project? project = null;

        if (projectId.HasValue)
        {
            project = await db.Projects
                .Include(item => item.Core)
                .FirstOrDefaultAsync(item => item.Id == projectId.Value && item.SpaceId == spaceId, cancellationToken)
                ?? throw new ValidationException("O projeto informado não pertence ao espaço ativo.");

            if (coreId.HasValue && coreId.Value != project.CoreId)
            {
                throw new ValidationException("O projeto informado não pertence ao núcleo selecionado.");
            }

            core = project.Core;
            return new ProjectCoreSelection(project.CoreId, core, project.Id, project);
        }

        if (coreId.HasValue)
        {
            core = await db.Cores
                .FirstOrDefaultAsync(item => item.Id == coreId.Value && item.SpaceId == spaceId, cancellationToken)
                ?? throw new ValidationException("O núcleo informado não pertence ao espaço ativo.");
        }

        return new ProjectCoreSelection(core?.Id, core, null, null);
    }

    private async Task<ProjectCoreSelection> ResolveProjectCoreByNameAsync(
        Guid spaceId,
        string? coreName,
        string? projectName,
        CancellationToken cancellationToken)
    {
        var core = await ResolveCoreByNameAsync(spaceId, coreName, cancellationToken);
        var normalizedProjectName = NormalizeOptional(projectName);
        if (normalizedProjectName is null)
        {
            return new ProjectCoreSelection(core?.Id, core, null, null);
        }

        var normalizedProjectKey = NormalizeNameKey(normalizedProjectName);
        var selectedCoreId = core?.Id;
        var projectMatches = await db.Projects
            .AsNoTracking()
            .Include(item => item.Core)
            .Where(item =>
                item.SpaceId == spaceId &&
                item.Name.ToUpper() == normalizedProjectKey &&
                (!selectedCoreId.HasValue || item.CoreId == selectedCoreId.Value))
            .ToArrayAsync(cancellationToken);

        if (projectMatches.Length == 0)
        {
            throw new ValidationException(
                core is null
                    ? "O projeto informado não pertence ao espaço ativo."
                    : "O projeto informado não pertence ao núcleo selecionado.");
        }

        if (projectMatches.Length > 1)
        {
            throw new ValidationException(
                core is null
                    ? "Há mais de um projeto com esse nome no espaço ativo. Informe também o núcleo."
                    : "Há mais de um projeto com esse nome dentro do núcleo selecionado.");
        }

        var project = projectMatches[0];
        return new ProjectCoreSelection(project.CoreId, project.Core, project.Id, project);
    }

    private async Task<Core?> ResolveCoreByNameAsync(Guid spaceId, string? coreName, CancellationToken cancellationToken)
    {
        var normalizedCoreName = NormalizeOptional(coreName);
        if (normalizedCoreName is null)
        {
            return null;
        }

        var normalizedCoreKey = NormalizeNameKey(normalizedCoreName);
        var coreMatches = await db.Cores
            .AsNoTracking()
            .Where(item => item.SpaceId == spaceId && item.Name.ToUpper() == normalizedCoreKey)
            .ToArrayAsync(cancellationToken);

        if (coreMatches.Length == 0)
        {
            throw new ValidationException("O núcleo informado não pertence ao espaço ativo.");
        }

        if (coreMatches.Length > 1)
        {
            throw new ValidationException("Há mais de um núcleo com esse nome no espaço ativo.");
        }

        return coreMatches[0];
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year < 2000 || year > 9999)
        {
            throw new ValidationException("O ano do período financeiro é inválido.");
        }

        if (month < 1 || month > 12)
        {
            throw new ValidationException("O mês do período financeiro é inválido.");
        }
    }

    private static void EnsureReferenceDateBelongsToPeriod(DateOnly referenceDate, int year, int month)
    {
        if (referenceDate.Year != year || referenceDate.Month != month)
        {
            throw new ValidationException("A data de referência deve pertencer ao mês selecionado.");
        }
    }

    private static string NormalizeGenerateMode(string mode)
    {
        var normalized = NormalizeOptional(mode) ?? "missingOnly";
        return normalized switch
        {
            "missingOnly" => normalized,
            "duplicateAll" => normalized,
            _ => throw new ValidationException("O modo de geração deve ser 'missingOnly' ou 'duplicateAll'.")
        };
    }

    private static void ValidateRecurrence(FinanceRecurrence recurrence, int? dayOfMonth, int? monthOfYear)
    {
        if (dayOfMonth.HasValue && (dayOfMonth.Value < 1 || dayOfMonth.Value > 31))
        {
            throw new ValidationException("O dia de referência da recorrência deve estar entre 1 e 31.");
        }

        if (recurrence == FinanceRecurrence.Annual)
        {
            if (!monthOfYear.HasValue || monthOfYear.Value < 1 || monthOfYear.Value > 12)
            {
                throw new ValidationException("Informe o mês da recorrência anual entre 1 e 12.");
            }

            return;
        }

        if (monthOfYear.HasValue)
        {
            throw new ValidationException("A recorrência mensal não aceita mês fixo.");
        }
    }

    private static void ValidateStatementDates(DateOnly closingDate, DateOnly dueDate)
    {
        if (dueDate <= closingDate)
        {
            throw new ValidationException("A data de vencimento da fatura deve ser posterior ao fechamento.");
        }
    }

    private static void ValidateCardDay(int day, string message)
    {
        if (day < 1 || day > 31)
        {
            throw new ValidationException(message);
        }
    }

    private static void ValidateReferenceYear(int year)
    {
        if (year < 2000 || year > 9999)
        {
            throw new ValidationException("O ano da referência anual é inválido.");
        }
    }

    private static void ValidateAssetDetails(
        AssetType type,
        AssetPropertyDetailsRequest? propertyDetails,
        AssetVehicleDetailsRequest? vehicleDetails)
    {
        if (type == AssetType.Property && propertyDetails is null)
        {
            throw new ValidationException("Informe os detalhes do imóvel.");
        }

        if (type == AssetType.Vehicle && vehicleDetails is null)
        {
            throw new ValidationException("Informe os detalhes do veículo.");
        }
    }

    private void ApplyAssetDetails(
        Asset asset,
        AssetType type,
        AssetPropertyDetailsRequest? propertyDetails,
        AssetVehicleDetailsRequest? vehicleDetails)
    {
        if (type == AssetType.Property)
        {
            if (asset.VehicleDetails is not null)
            {
                db.AssetVehicleDetails.Remove(asset.VehicleDetails);
                asset.VehicleDetails = null;
            }

            asset.PropertyDetails ??= new AssetPropertyDetails { Asset = asset };
            asset.PropertyDetails.RegistryNumber = NormalizeOptional(propertyDetails?.RegistryNumber);
            asset.PropertyDetails.PropertyInscription = NormalizeOptional(propertyDetails?.PropertyInscription);
            asset.PropertyDetails.PrivateAreaSquareMeters = propertyDetails?.PrivateAreaSquareMeters;
            asset.PropertyDetails.DebtCheckOn = propertyDetails?.DebtCheckOn;
            return;
        }

        if (type == AssetType.Vehicle)
        {
            if (asset.PropertyDetails is not null)
            {
                db.AssetPropertyDetails.Remove(asset.PropertyDetails);
                asset.PropertyDetails = null;
            }

            asset.VehicleDetails ??= new AssetVehicleDetails { Asset = asset };
            asset.VehicleDetails.Brand = NormalizeOptional(vehicleDetails?.Brand);
            asset.VehicleDetails.Model = NormalizeOptional(vehicleDetails?.Model);
            asset.VehicleDetails.YearModel = NormalizeOptional(vehicleDetails?.YearModel);
            asset.VehicleDetails.Renavam = NormalizeOptional(vehicleDetails?.Renavam);
            return;
        }

        if (asset.PropertyDetails is not null)
        {
            db.AssetPropertyDetails.Remove(asset.PropertyDetails);
            asset.PropertyDetails = null;
        }

        if (asset.VehicleDetails is not null)
        {
            db.AssetVehicleDetails.Remove(asset.VehicleDetails);
            asset.VehicleDetails = null;
        }
    }

    private static void ValidateAmount(decimal? amount, string message)
    {
        if (amount.HasValue && amount.Value < 0)
        {
            throw new ValidationException(message);
        }
    }

    private static void ValidatePositiveAmount(decimal amount, string message)
    {
        if (amount <= 0)
        {
            throw new ValidationException(message);
        }
    }

    private static string? NormalizeLastFourDigits(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length != 4 || normalized.Any(character => !char.IsDigit(character)))
        {
            throw new ValidationException("Os quatro últimos dígitos do cartão devem ter exatamente 4 números.");
        }

        return normalized;
    }

    private static AssetDto ToAssetDto(Asset asset, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, asset.CreatedByMemberId);
        return new AssetDto(
            asset.Id,
            asset.Title,
            asset.Type,
            asset.CurrentValue,
            asset.RemainingDebt,
            asset.IsPaidOff,
            asset.Notes,
            asset.PropertyDetails is null
                ? null
                : new AssetPropertyDetailsDto(
                    asset.PropertyDetails.RegistryNumber,
                    asset.PropertyDetails.PropertyInscription,
                    asset.PropertyDetails.PrivateAreaSquareMeters,
                    asset.PropertyDetails.DebtCheckOn),
            asset.VehicleDetails is null
                ? null
                : new AssetVehicleDetailsDto(
                    asset.VehicleDetails.Brand,
                    asset.VehicleDetails.Model,
                    asset.VehicleDetails.YearModel,
                    asset.VehicleDetails.Renavam),
            asset.CreatedByMemberId,
            asset.CreatedAt,
            asset.UpdatedAt,
            canManage,
            canManage);
    }

    private static AssetValuationDto ToAssetValuationDto(AssetValuation valuation, bool canManage)
    {
        return new AssetValuationDto(
            valuation.Id,
            valuation.AssetId,
            valuation.ReferenceYear,
            valuation.Label,
            valuation.Amount,
            valuation.Notes,
            valuation.CreatedAt,
            valuation.UpdatedAt,
            canManage,
            canManage);
    }

    private static FinanceRecurringTemplateDto ToRecurringTemplateDto(FinanceRecurringTemplate template, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, template.CreatedByMemberId);
        return new FinanceRecurringTemplateDto(
            template.Id,
            template.Title,
            template.Notes,
            template.Type,
            template.DefaultAmount,
            template.Recurrence,
            template.DayOfMonth,
            template.MonthOfYear,
            template.IsActive,
            template.CategoryId,
            template.Category?.Name,
            template.CoreId,
            template.Core?.Name,
            template.ProjectId,
            template.Project?.Name,
            template.CreatedByMemberId,
            template.CreatedAt,
            template.UpdatedAt,
            canManage,
            canManage);
    }

    private static FinanceEntryDto ToEntryDto(FinanceEntry entry, SpaceMember currentMember)
    {
        var canManage = entry.Origin != FinanceEntryOrigin.CreditCardStatement && CanManageEntity(currentMember, entry.CreatedByMemberId);
        return new FinanceEntryDto(
            entry.Id,
            entry.FinancePeriodId,
            entry.FinancePeriod?.Year ?? entry.ReferenceDate.Year,
            entry.FinancePeriod?.Month ?? entry.ReferenceDate.Month,
            entry.Title,
            entry.Notes,
            entry.Amount,
            entry.Type,
            IsVerifiedEntry(entry),
            entry.ReferenceDate,
            entry.Origin,
            entry.RecurringTemplateId,
            entry.CreditCardStatementId,
            entry.CategoryId,
            entry.Category?.Name,
            entry.CoreId,
            entry.Core?.Name,
            entry.ProjectId,
            entry.Project?.Name,
            entry.CreatedByMemberId,
            entry.CreatedAt,
            entry.UpdatedAt,
            canManage,
            canManage);
    }

    private static CreditCardAccountDto ToCreditCardAccountDto(CreditCardAccount account, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, account.CreatedByMemberId);
        var openTransactions = account.Transactions.Where(item => item.CreditCardStatementId == null).ToArray();
        return new CreditCardAccountDto(
            account.Id,
            account.Name,
            account.Brand,
            account.LastFourDigits,
            account.ClosingDay,
            account.DueDay,
            account.Notes,
            account.IsActive,
            openTransactions.Length,
            openTransactions.Sum(item => item.Amount),
            account.CreatedByMemberId,
            account.CreatedAt,
            account.UpdatedAt,
            canManage,
            canManage);
    }

    private static CreditCardTransactionDto ToTransactionDto(CreditCardTransaction transaction, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, transaction.CreatedByMemberId);
        return new CreditCardTransactionDto(
            transaction.Id,
            transaction.CreditCardAccountId,
            transaction.CreditCardAccount?.Name ?? string.Empty,
            transaction.CreditCardStatementId,
            transaction.Title,
            transaction.Merchant,
            transaction.Amount,
            transaction.PurchasedOn,
            transaction.Notes,
            transaction.CategoryId,
            transaction.Category?.Name,
            transaction.CoreId,
            transaction.Core?.Name,
            transaction.ProjectId,
            transaction.Project?.Name,
            transaction.ExternalSource,
            transaction.ExternalReference,
            transaction.ImportedAt,
            transaction.CreatedByMemberId,
            transaction.CreatedAt,
            transaction.UpdatedAt,
            canManage,
            canManage);
    }

    private static CreditCardStatementDto ToStatementDto(CreditCardStatement statement, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, statement.CreatedByMemberId);
        return new CreditCardStatementDto(
            statement.Id,
            statement.CreditCardAccountId,
            statement.CreditCardAccount?.Name ?? string.Empty,
            statement.ClosingDate,
            statement.DueDate,
            statement.TotalAmount,
            statement.Notes,
            statement.Transactions.Count,
            statement.FinanceEntry?.Id,
            statement.ExternalSource,
            statement.ExternalReference,
            statement.ImportedAt,
            statement.CreatedByMemberId,
            statement.CreatedAt,
            statement.UpdatedAt,
            canManage,
            canManage);
    }

    private static bool IsVerifiedEntry(FinanceEntry entry)
        => entry.Verified || entry.Origin == FinanceEntryOrigin.CreditCardStatement;

    private static FinanceCategoryDto ToCategoryDto(
        FinanceCategory category,
        SpaceMember currentMember,
        int usageCount)
    {
        var canManage = !category.IsDefault && CanManageEntity(currentMember, category.CreatedByMemberId);
        return new FinanceCategoryDto(
            category.Id,
            category.Name,
            category.IsDefault,
            category.SortOrder,
            category.CreatedByMemberId,
            usageCount,
            canManage,
            canManage);
    }

    private async Task<Guid> ResolveSpaceIdAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return await ResolveSuperAdminSpaceIdAsync(cancellationToken);
        }

        var memberships = await db.SpaceMembers
            .AsNoTracking()
            .Where(member => member.UserId == userContext.UserId && member.IsActive)
            .Select(member => member.SpaceId)
            .ToArrayAsync(cancellationToken);

        if (memberships.Length == 0)
        {
            throw new ForbiddenException("Usuário sem espaço vinculado.");
        }

        if (userContext.SpaceId is null)
        {
            if (memberships.Length == 1)
            {
                return memberships[0];
            }

            throw new ValidationException("Informe X-Space-Id para escolher o espaço.");
        }

        if (!memberships.Contains(userContext.SpaceId.Value))
        {
            throw new ForbiddenException("Você não tem acesso a este espaço.");
        }

        return userContext.SpaceId.Value;
    }

    private async Task<SpaceMember> ResolveCurrentMemberAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return new SpaceMember
            {
                SpaceId = spaceId,
                UserId = userContext.UserId,
                Role = SpaceRole.Member
            };
        }

        return await db.SpaceMembers
            .Include(member => member.User)
            .FirstOrDefaultAsync(member =>
                member.SpaceId == spaceId &&
                member.UserId == userContext.UserId &&
                member.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a este espaço.");
    }

    private async Task<SpaceMember> ResolveCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var spaceId = await ResolveSpaceIdAsync(cancellationToken);
        return await ResolveCurrentMemberAsync(spaceId, cancellationToken);
    }

    private async Task<Guid> ResolveSuperAdminSpaceIdAsync(CancellationToken cancellationToken)
    {
        if (userContext.SpaceId is null)
        {
            var spaceIds = await db.Spaces
                .AsNoTracking()
                .OrderBy(space => space.Name)
                .Select(space => space.Id)
                .Take(2)
                .ToArrayAsync(cancellationToken);

            return spaceIds.Length switch
            {
                0 => throw new NotFoundException("Espaço não encontrada."),
                1 => spaceIds[0],
                _ => throw new ValidationException("Informe X-Space-Id para escolher o espaço.")
            };
        }

        var exists = await db.Spaces
            .AsNoTracking()
            .AnyAsync(space => space.Id == userContext.SpaceId.Value, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Espaço não encontrada.");
        }

        return userContext.SpaceId.Value;
    }

    private static string RequiredText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message);
        }

        return value.Trim();
    }

    private static string NormalizeNameKey(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsContentManager(SpaceMember member)
    {
        return member.Role is SpaceRole.Owner or SpaceRole.Admin;
    }

    private static bool CanManageEntity(SpaceMember member, Guid? createdByMemberId)
    {
        return IsContentManager(member) || createdByMemberId == member.Id;
    }

    private static void EnsureCanManageEntity(SpaceMember member, Guid? createdByMemberId, string message)
    {
        if (!CanManageEntity(member, createdByMemberId))
        {
            throw new ForbiddenException(message);
        }
    }

    private void EnsureWritable()
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            throw new ForbiddenException(SuperAdminReadOnlyMessage);
        }
    }

    private void ApplyExpectedVersion(AuditableEntity entity, DateTimeOffset? expectedUpdatedAt)
    {
        if (expectedUpdatedAt.HasValue)
        {
            db.SetExpectedUpdatedAt(entity, expectedUpdatedAt.Value);
        }
    }

    private sealed record ProjectCoreSelection(
        Guid? CoreId,
        Core? Core,
        Guid? ProjectId,
        Project? Project);
}
