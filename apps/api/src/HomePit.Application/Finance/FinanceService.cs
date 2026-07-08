using HomePit.Application.Common;
using HomePit.Domain.Finance;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Finance;

public sealed class FinanceService(
    IHomePitDbContext db,
    IUserContext userContext)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    public async Task<IReadOnlyCollection<FinancePeriodListItemDto>> ListPeriodsAsync(CancellationToken cancellationToken)
    {
        var householdId = await ResolveHouseholdIdAsync(cancellationToken);
        var periods = await db.FinancePeriods
            .AsNoTracking()
            .Include(period => period.Entries)
            .Where(period => period.HouseholdId == householdId)
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
            .FirstOrDefaultAsync(item => item.HouseholdId == currentMember.HouseholdId && item.Year == year && item.Month == month, cancellationToken);

        var entries = await db.FinanceEntries
            .AsNoTracking()
            .Include(entry => entry.FinancePeriod)
            .Include(entry => entry.Category)
            .Include(entry => entry.Universe)
            .Include(entry => entry.Project)
            .Where(entry => entry.HouseholdId == currentMember.HouseholdId && entry.FinancePeriod!.Year == year && entry.FinancePeriod.Month == month)
            .OrderBy(entry => entry.ReferenceDate)
            .ThenBy(entry => entry.Type)
            .ThenBy(entry => entry.Title)
            .ToArrayAsync(cancellationToken);

        var transactions = await db.CreditCardTransactions
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.Category)
            .Include(item => item.Universe)
            .Include(item => item.Project)
            .Where(item => item.HouseholdId == currentMember.HouseholdId && item.PurchasedOn.Year == year && item.PurchasedOn.Month == month)
            .OrderByDescending(item => item.PurchasedOn)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);

        var statements = await db.CreditCardStatements
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.FinanceEntry)
            .Include(item => item.Transactions)
            .Where(item => item.HouseholdId == currentMember.HouseholdId && item.DueDate.Year == year && item.DueDate.Month == month)
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
                entries.Count(entry => entry.Verified),
                entries.Count(entry => !entry.Verified),
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
        var period = await GetOrCreatePeriodAsync(currentMember.HouseholdId, year, month, cancellationToken);

        var templates = await db.FinanceRecurringTemplates
            .Where(item => item.HouseholdId == currentMember.HouseholdId && item.IsActive)
            .OrderBy(item => item.Title)
            .ToArrayAsync(cancellationToken);

        var existingTemplateIds = await db.FinanceEntries
            .Where(item => item.HouseholdId == currentMember.HouseholdId && item.FinancePeriodId == period.Id && item.RecurringTemplateId != null)
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
                HouseholdId = currentMember.HouseholdId,
                FinancePeriodId = period.Id,
                CreatedByMemberId = currentMember.Id,
                RecurringTemplateId = template.Id,
                CategoryId = template.CategoryId,
                UniverseId = template.UniverseId,
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
            .Where(item => item.HouseholdId == currentMember.HouseholdId)
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
        await EnsureFinanceCategoryNameAvailableAsync(currentMember.HouseholdId, name, null, cancellationToken);

        var customSortOrder = await db.FinanceCategories
            .Where(item => item.HouseholdId == currentMember.HouseholdId && !item.IsDefault)
            .Select(item => (int?)item.SortOrder)
            .MaxAsync(cancellationToken) ?? (FinanceCategoryCatalog.DefaultNames.Count - 1);

        var category = new FinanceCategory
        {
            HouseholdId = currentMember.HouseholdId,
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
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = await db.FinanceCategories
            .Include(item => item.Entries)
            .Include(item => item.RecurringTemplates)
            .Include(item => item.CreditCardTransactions)
            .FirstOrDefaultAsync(item => item.Id == categoryId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Categoria não encontrada.");

        if (category.IsDefault)
        {
            throw new ValidationException("Categorias padrão não podem ser editadas.");
        }

        EnsureCanManageEntity(currentMember, category.CreatedByMemberId, "Você não pode editar uma categoria criada por outra pessoa.");

        var name = RequiredText(request.Name, "Informe o nome da categoria.");
        await EnsureFinanceCategoryNameAvailableAsync(currentMember.HouseholdId, name, category.Id, cancellationToken);

        category.Name = name;
        await db.SaveChangesAsync(cancellationToken);

        var usageCount = category.Entries.Count + category.RecurringTemplates.Count + category.CreditCardTransactions.Count;
        return new FinanceCategoryDto(category.Id, category.Name, false, category.SortOrder, category.CreatedByMemberId, usageCount, true, true);
    }

    public async Task DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = await db.FinanceCategories
            .FirstOrDefaultAsync(item => item.Id == categoryId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Categoria não encontrada.");

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
            .Include(item => item.Universe)
            .Include(item => item.Project)
            .Where(item => item.HouseholdId == currentMember.HouseholdId)
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
        var selection = await ResolveProjectUniverseAsync(currentMember.HouseholdId, request.UniverseId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.HouseholdId, request.CategoryId, cancellationToken);

        ValidateAmount(request.DefaultAmount, "O valor padrão da recorrência não pode ser negativo.");
        ValidateRecurrence(request.Recurrence, request.DayOfMonth, request.MonthOfYear);

        var template = new FinanceRecurringTemplate
        {
            HouseholdId = currentMember.HouseholdId,
            CreatedByMemberId = currentMember.Id,
            UniverseId = selection.UniverseId,
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
        template.Universe = selection.Universe;
        template.Project = selection.Project;
        return ToRecurringTemplateDto(template, currentMember);
    }

    public async Task<FinanceRecurringTemplateDto> UpdateRecurringTemplateAsync(
        Guid templateId,
        UpdateFinanceRecurringTemplateRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var template = await db.FinanceRecurringTemplates
            .Include(item => item.Category)
            .Include(item => item.Universe)
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == templateId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Recorrência não encontrada.");

        EnsureCanManageEntity(currentMember, template.CreatedByMemberId, "Você não pode editar uma recorrência criada por outra pessoa.");

        var selection = await ResolveProjectUniverseAsync(currentMember.HouseholdId, request.UniverseId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.HouseholdId, request.CategoryId, cancellationToken);
        ValidateAmount(request.DefaultAmount, "O valor padrão da recorrência não pode ser negativo.");
        ValidateRecurrence(request.Recurrence, request.DayOfMonth, request.MonthOfYear);

        template.UniverseId = selection.UniverseId;
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
        template.Universe = selection.Universe;
        template.Project = selection.Project;
        return ToRecurringTemplateDto(template, currentMember);
    }

    public async Task DeleteRecurringTemplateAsync(Guid templateId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var template = await db.FinanceRecurringTemplates
            .FirstOrDefaultAsync(item => item.Id == templateId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Recorrência não encontrada.");

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
            .Include(entry => entry.Universe)
            .Include(entry => entry.Project)
            .Where(entry => entry.HouseholdId == currentMember.HouseholdId);

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
        var period = await GetOrCreatePeriodAsync(currentMember.HouseholdId, request.Year, request.Month, cancellationToken);
        var selection = await ResolveProjectUniverseAsync(currentMember.HouseholdId, request.UniverseId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.HouseholdId, request.CategoryId, cancellationToken);
        await EnsureRecurringTemplateAsync(currentMember.HouseholdId, request.RecurringTemplateId, cancellationToken);

        ValidateAmount(request.Amount, "O valor do lançamento não pode ser negativo.");
        EnsureReferenceDateBelongsToPeriod(request.ReferenceDate, request.Year, request.Month);

        var entry = new FinanceEntry
        {
            HouseholdId = currentMember.HouseholdId,
            FinancePeriodId = period.Id,
            CreatedByMemberId = currentMember.Id,
            RecurringTemplateId = request.RecurringTemplateId,
            UniverseId = selection.UniverseId,
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
        entry.Universe = selection.Universe;
        entry.Project = selection.Project;
        return ToEntryDto(entry, currentMember);
    }

    public async Task<FinanceEntryDto> UpdateEntryAsync(
        Guid entryId,
        UpdateFinanceEntryRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        ValidatePeriod(request.Year, request.Month);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var entry = await db.FinanceEntries
            .Include(item => item.FinancePeriod)
            .Include(item => item.Category)
            .Include(item => item.Universe)
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == entryId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Lançamento não encontrado.");

        if (entry.Origin == FinanceEntryOrigin.CreditCardStatement)
        {
            throw new ValidationException("A fatura consolidada deve ser alterada pelo cartão de crédito.");
        }

        EnsureCanManageEntity(currentMember, entry.CreatedByMemberId, "Você não pode editar um lançamento criado por outra pessoa.");

        var period = await GetOrCreatePeriodAsync(currentMember.HouseholdId, request.Year, request.Month, cancellationToken);
        var selection = await ResolveProjectUniverseAsync(currentMember.HouseholdId, request.UniverseId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.HouseholdId, request.CategoryId, cancellationToken);
        await EnsureRecurringTemplateAsync(currentMember.HouseholdId, request.RecurringTemplateId, cancellationToken);
        ValidateAmount(request.Amount, "O valor do lançamento não pode ser negativo.");
        EnsureReferenceDateBelongsToPeriod(request.ReferenceDate, request.Year, request.Month);

        entry.FinancePeriodId = period.Id;
        entry.FinancePeriod = period;
        entry.UniverseId = selection.UniverseId;
        entry.ProjectId = selection.ProjectId;
        entry.CategoryId = category?.Id;
        entry.Universe = selection.Universe;
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

    public async Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var entry = await db.FinanceEntries
            .FirstOrDefaultAsync(item => item.Id == entryId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Lançamento não encontrado.");

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
            .Where(item => item.HouseholdId == currentMember.HouseholdId)
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
            HouseholdId = currentMember.HouseholdId,
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

    public async Task<AssetDto> UpdateAssetAsync(Guid assetId, UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .Include(item => item.PropertyDetails)
            .Include(item => item.VehicleDetails)
            .FirstOrDefaultAsync(item => item.Id == assetId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");

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

    public async Task DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .FirstOrDefaultAsync(item => item.Id == assetId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");

        EnsureCanManageEntity(currentMember, asset.CreatedByMemberId, "Você não pode excluir um bem criado por outra pessoa.");
        db.Assets.Remove(asset);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssetValuationDto>> ListAssetValuationsAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == assetId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
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
            .FirstOrDefaultAsync(item => item.Id == assetId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
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
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == assetId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");

        EnsureCanManageEntity(currentMember, asset.CreatedByMemberId, "Você não pode editar uma avaliação de um bem criado por outra pessoa.");

        var valuation = await db.AssetValuations
            .FirstOrDefaultAsync(item => item.Id == valuationId && item.AssetId == assetId, cancellationToken)
            ?? throw new NotFoundException("Avaliação não encontrada.");

        ValidateReferenceYear(request.ReferenceYear);
        ValidatePositiveAmount(request.Amount, "O valor de referência deve ser maior que zero.");

        valuation.ReferenceYear = request.ReferenceYear;
        valuation.Label = RequiredText(request.Label, "Informe o rótulo da referência anual.");
        valuation.Amount = request.Amount;
        valuation.Notes = NormalizeOptional(request.Notes);

        await db.SaveChangesAsync(cancellationToken);
        return ToAssetValuationDto(valuation, true);
    }

    public async Task DeleteAssetValuationAsync(Guid assetId, Guid valuationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var asset = await db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == assetId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Bem não encontrado.");

        EnsureCanManageEntity(currentMember, asset.CreatedByMemberId, "Você não pode excluir uma avaliação de um bem criado por outra pessoa.");

        var valuation = await db.AssetValuations
            .FirstOrDefaultAsync(item => item.Id == valuationId && item.AssetId == assetId, cancellationToken)
            ?? throw new NotFoundException("Avaliação não encontrada.");

        db.AssetValuations.Remove(valuation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CreditCardAccountDto>> ListCreditCardAccountsAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var accounts = await db.CreditCardAccounts
            .AsNoTracking()
            .Include(account => account.Transactions)
            .Where(account => account.HouseholdId == currentMember.HouseholdId)
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
            HouseholdId = currentMember.HouseholdId,
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
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var account = await db.CreditCardAccounts
            .Include(item => item.Transactions)
            .FirstOrDefaultAsync(item => item.Id == accountId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Cartão não encontrado.");

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

    public async Task DeleteCreditCardAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var account = await db.CreditCardAccounts
            .Include(item => item.Statements)
            .FirstOrDefaultAsync(item => item.Id == accountId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Cartão não encontrado.");

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
        await EnsureCreditCardAccountAsync(currentMember.HouseholdId, accountId, cancellationToken);

        var transactions = await db.CreditCardTransactions
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.Category)
            .Include(item => item.Universe)
            .Include(item => item.Project)
            .Where(item => item.HouseholdId == currentMember.HouseholdId && item.CreditCardAccountId == accountId)
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
        await EnsureCreditCardAccountAsync(currentMember.HouseholdId, accountId, cancellationToken);
        var selection = await ResolveProjectUniverseAsync(currentMember.HouseholdId, request.UniverseId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.HouseholdId, request.CategoryId, cancellationToken);
        ValidatePositiveAmount(request.Amount, "O valor da compra no cartão deve ser maior que zero.");

        var transaction = new CreditCardTransaction
        {
            HouseholdId = currentMember.HouseholdId,
            CreditCardAccountId = accountId,
            CreatedByMemberId = currentMember.Id,
            UniverseId = selection.UniverseId,
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
        transaction.Universe = selection.Universe;
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
        await EnsureCreditCardAccountAsync(currentMember.HouseholdId, accountId, cancellationToken);

        if (request.Transactions is null || request.Transactions.Count == 0)
        {
            throw new ValidationException("Envie ao menos uma compra para importar.");
        }

        var existingCategories = await db.FinanceCategories
            .AsNoTracking()
            .Where(item => item.HouseholdId == currentMember.HouseholdId)
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
            var selection = await ResolveProjectUniverseByNameAsync(currentMember.HouseholdId, item.UniverseName, item.ProjectName, cancellationToken);
            var category = ResolveOrCreateFinanceCategoryByName(
                currentMember.HouseholdId,
                currentMember.Id,
                categoriesByNormalizedName,
                ref nextCategorySortOrder,
                ref createdCategoryCount,
                item.CategoryName);
            ValidatePositiveAmount(item.Amount, "O valor da compra no cartão deve ser maior que zero.");

            var transaction = new CreditCardTransaction
            {
                HouseholdId = currentMember.HouseholdId,
                CreditCardAccountId = accountId,
                CreatedByMemberId = currentMember.Id,
                UniverseId = selection.UniverseId,
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
            .Include(item => item.Universe)
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
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var transaction = await db.CreditCardTransactions
            .Include(item => item.CreditCardAccount)
            .Include(item => item.Category)
            .Include(item => item.Universe)
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item =>
                item.Id == transactionId &&
                item.CreditCardAccountId == accountId &&
                item.HouseholdId == currentMember.HouseholdId,
                cancellationToken)
            ?? throw new NotFoundException("Compra no cartão não encontrada.");

        EnsureCanManageEntity(currentMember, transaction.CreatedByMemberId, "Você não pode editar uma compra no cartão criada por outra pessoa.");

        var selection = await ResolveProjectUniverseAsync(currentMember.HouseholdId, request.UniverseId, request.ProjectId, cancellationToken);
        var category = await ResolveFinanceCategoryAsync(currentMember.HouseholdId, request.CategoryId, cancellationToken);
        ValidatePositiveAmount(request.Amount, "O valor da compra no cartão deve ser maior que zero.");

        transaction.UniverseId = selection.UniverseId;
        transaction.ProjectId = selection.ProjectId;
        transaction.CategoryId = category?.Id;
        transaction.Universe = selection.Universe;
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

    public async Task DeleteCreditCardTransactionAsync(Guid accountId, Guid transactionId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var transaction = await db.CreditCardTransactions
            .FirstOrDefaultAsync(item =>
                item.Id == transactionId &&
                item.CreditCardAccountId == accountId &&
                item.HouseholdId == currentMember.HouseholdId,
                cancellationToken)
            ?? throw new NotFoundException("Compra no cartão não encontrada.");

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
        await EnsureCreditCardAccountAsync(currentMember.HouseholdId, accountId, cancellationToken);

        var statements = await db.CreditCardStatements
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.FinanceEntry)
            .Include(item => item.Transactions)
            .Where(item => item.HouseholdId == currentMember.HouseholdId && item.CreditCardAccountId == accountId)
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
            .FirstOrDefaultAsync(item => item.Id == accountId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Cartão não encontrado.");

        ValidateStatementDates(request.ClosingDate, request.DueDate);

        var statement = new CreditCardStatement
        {
            HouseholdId = currentMember.HouseholdId,
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

        await AssignTransactionsToStatementAsync(currentMember.HouseholdId, account.Id, statement.Id, request.TransactionIds, cancellationToken);
        await RecalculateStatementAsync(statement.Id, cancellationToken);

        return await GetStatementAsync(statement.Id, currentMember, cancellationToken);
    }

    public async Task<CreditCardStatementDto> UpdateCreditCardStatementAsync(
        Guid accountId,
        Guid statementId,
        UpdateCreditCardStatementRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var statement = await db.CreditCardStatements
            .Include(item => item.CreditCardAccount)
            .FirstOrDefaultAsync(item =>
                item.Id == statementId &&
                item.CreditCardAccountId == accountId &&
                item.HouseholdId == currentMember.HouseholdId,
                cancellationToken)
            ?? throw new NotFoundException("Fatura não encontrada.");

        EnsureCanManageEntity(currentMember, statement.CreatedByMemberId, "Você não pode editar uma fatura criada por outra pessoa.");
        ValidateStatementDates(request.ClosingDate, request.DueDate);

        statement.ClosingDate = request.ClosingDate;
        statement.DueDate = request.DueDate;
        statement.Notes = NormalizeOptional(request.Notes);
        statement.ExternalSource = NormalizeOptional(request.ExternalSource);
        statement.ExternalReference = NormalizeOptional(request.ExternalReference);
        statement.ImportedAt = request.ImportedAt;

        await db.SaveChangesAsync(cancellationToken);
        await AssignTransactionsToStatementAsync(currentMember.HouseholdId, accountId, statement.Id, request.TransactionIds, cancellationToken);
        await RecalculateStatementAsync(statement.Id, cancellationToken);

        return await GetStatementAsync(statement.Id, currentMember, cancellationToken);
    }

    public async Task DeleteCreditCardStatementAsync(Guid accountId, Guid statementId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var statement = await db.CreditCardStatements
            .Include(item => item.Transactions)
            .FirstOrDefaultAsync(item =>
                item.Id == statementId &&
                item.CreditCardAccountId == accountId &&
                item.HouseholdId == currentMember.HouseholdId,
                cancellationToken)
            ?? throw new NotFoundException("Fatura não encontrada.");

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
        HouseholdMember currentMember,
        CancellationToken cancellationToken)
    {
        var statement = await db.CreditCardStatements
            .AsNoTracking()
            .Include(item => item.CreditCardAccount)
            .Include(item => item.FinanceEntry)
            .Include(item => item.Transactions)
            .FirstOrDefaultAsync(item => item.Id == statementId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Fatura não encontrada.");

        return ToStatementDto(statement, currentMember);
    }

    private async Task AssignTransactionsToStatementAsync(
        Guid householdId,
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
                item.HouseholdId == householdId &&
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

        var period = await GetOrCreatePeriodAsync(statement.HouseholdId, statement.DueDate.Year, statement.DueDate.Month, cancellationToken);
        var generatedEntry = await db.FinanceEntries
            .FirstOrDefaultAsync(entry => entry.CreditCardStatementId == statement.Id, cancellationToken);

        var entryTitle = $"Fatura {statement.CreditCardAccount!.Name} - {statement.DueDate:MM/yyyy}";
        if (generatedEntry is null)
        {
            generatedEntry = new FinanceEntry
            {
                HouseholdId = statement.HouseholdId,
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
            generatedEntry.ReferenceDate = statement.DueDate;
            generatedEntry.Origin = FinanceEntryOrigin.CreditCardStatement;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCreditCardAccountAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken)
    {
        var exists = await db.CreditCardAccounts
            .AnyAsync(item => item.Id == accountId && item.HouseholdId == householdId, cancellationToken);

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

    private async Task EnsureRecurringTemplateAsync(Guid householdId, Guid? recurringTemplateId, CancellationToken cancellationToken)
    {
        if (!recurringTemplateId.HasValue)
        {
            return;
        }

        var exists = await db.FinanceRecurringTemplates
            .AnyAsync(item => item.Id == recurringTemplateId.Value && item.HouseholdId == householdId, cancellationToken);

        if (!exists)
        {
            throw new ValidationException("A recorrência informada não pertence a esta casa.");
        }
    }

    private async Task<FinanceCategory?> ResolveFinanceCategoryAsync(Guid householdId, Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        return await db.FinanceCategories
            .FirstOrDefaultAsync(item => item.Id == categoryId.Value && item.HouseholdId == householdId, cancellationToken)
            ?? throw new ValidationException("A categoria informada não pertence à casa ativa.");
    }

    private FinanceCategory? ResolveOrCreateFinanceCategoryByName(
        Guid householdId,
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
            HouseholdId = householdId,
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
        Guid householdId,
        string name,
        Guid? currentCategoryId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.ToUpperInvariant();
        var exists = await db.FinanceCategories
            .AnyAsync(
                item =>
                    item.HouseholdId == householdId &&
                    item.Id != currentCategoryId &&
                    item.Name.ToUpper() == normalizedName,
                cancellationToken);

        if (exists)
        {
            throw new ValidationException("Já existe uma categoria com esse nome nesta casa.");
        }
    }

    private async Task<FinancePeriod> GetOrCreatePeriodAsync(Guid householdId, int year, int month, CancellationToken cancellationToken)
    {
        var period = await db.FinancePeriods
            .FirstOrDefaultAsync(item => item.HouseholdId == householdId && item.Year == year && item.Month == month, cancellationToken);

        if (period is not null)
        {
            return period;
        }

        period = new FinancePeriod
        {
            HouseholdId = householdId,
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

    private async Task<ProjectUniverseSelection> ResolveProjectUniverseAsync(
        Guid householdId,
        Guid? universeId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        Universe? universe = null;
        Project? project = null;

        if (projectId.HasValue)
        {
            project = await db.Projects
                .Include(item => item.Universe)
                .FirstOrDefaultAsync(item => item.Id == projectId.Value && item.HouseholdId == householdId, cancellationToken)
                ?? throw new ValidationException("O projeto informado não pertence à casa ativa.");

            if (universeId.HasValue && universeId.Value != project.UniverseId)
            {
                throw new ValidationException("O projeto informado não pertence ao universo selecionado.");
            }

            universe = project.Universe;
            return new ProjectUniverseSelection(project.UniverseId, universe, project.Id, project);
        }

        if (universeId.HasValue)
        {
            universe = await db.Universes
                .FirstOrDefaultAsync(item => item.Id == universeId.Value && item.HouseholdId == householdId, cancellationToken)
                ?? throw new ValidationException("O universo informado não pertence à casa ativa.");
        }

        return new ProjectUniverseSelection(universe?.Id, universe, null, null);
    }

    private async Task<ProjectUniverseSelection> ResolveProjectUniverseByNameAsync(
        Guid householdId,
        string? universeName,
        string? projectName,
        CancellationToken cancellationToken)
    {
        var universe = await ResolveUniverseByNameAsync(householdId, universeName, cancellationToken);
        var normalizedProjectName = NormalizeOptional(projectName);
        if (normalizedProjectName is null)
        {
            return new ProjectUniverseSelection(universe?.Id, universe, null, null);
        }

        var normalizedProjectKey = NormalizeNameKey(normalizedProjectName);
        var selectedUniverseId = universe?.Id;
        var projectMatches = await db.Projects
            .AsNoTracking()
            .Include(item => item.Universe)
            .Where(item =>
                item.HouseholdId == householdId &&
                item.Name.ToUpper() == normalizedProjectKey &&
                (!selectedUniverseId.HasValue || item.UniverseId == selectedUniverseId.Value))
            .ToArrayAsync(cancellationToken);

        if (projectMatches.Length == 0)
        {
            throw new ValidationException(
                universe is null
                    ? "O projeto informado não pertence à casa ativa."
                    : "O projeto informado não pertence ao universo selecionado.");
        }

        if (projectMatches.Length > 1)
        {
            throw new ValidationException(
                universe is null
                    ? "Há mais de um projeto com esse nome na casa ativa. Informe também o universo."
                    : "Há mais de um projeto com esse nome dentro do universo selecionado.");
        }

        var project = projectMatches[0];
        return new ProjectUniverseSelection(project.UniverseId, project.Universe, project.Id, project);
    }

    private async Task<Universe?> ResolveUniverseByNameAsync(Guid householdId, string? universeName, CancellationToken cancellationToken)
    {
        var normalizedUniverseName = NormalizeOptional(universeName);
        if (normalizedUniverseName is null)
        {
            return null;
        }

        var normalizedUniverseKey = NormalizeNameKey(normalizedUniverseName);
        var universeMatches = await db.Universes
            .AsNoTracking()
            .Where(item => item.HouseholdId == householdId && item.Name.ToUpper() == normalizedUniverseKey)
            .ToArrayAsync(cancellationToken);

        if (universeMatches.Length == 0)
        {
            throw new ValidationException("O universo informado não pertence à casa ativa.");
        }

        if (universeMatches.Length > 1)
        {
            throw new ValidationException("Há mais de um universo com esse nome na casa ativa.");
        }

        return universeMatches[0];
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

    private static AssetDto ToAssetDto(Asset asset, HouseholdMember currentMember)
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

    private static FinanceRecurringTemplateDto ToRecurringTemplateDto(FinanceRecurringTemplate template, HouseholdMember currentMember)
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
            template.UniverseId,
            template.Universe?.Name,
            template.ProjectId,
            template.Project?.Name,
            template.CreatedByMemberId,
            template.CreatedAt,
            template.UpdatedAt,
            canManage,
            canManage);
    }

    private static FinanceEntryDto ToEntryDto(FinanceEntry entry, HouseholdMember currentMember)
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
            entry.Verified,
            entry.ReferenceDate,
            entry.Origin,
            entry.RecurringTemplateId,
            entry.CreditCardStatementId,
            entry.CategoryId,
            entry.Category?.Name,
            entry.UniverseId,
            entry.Universe?.Name,
            entry.ProjectId,
            entry.Project?.Name,
            entry.CreatedByMemberId,
            entry.CreatedAt,
            entry.UpdatedAt,
            canManage,
            canManage);
    }

    private static CreditCardAccountDto ToCreditCardAccountDto(CreditCardAccount account, HouseholdMember currentMember)
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

    private static CreditCardTransactionDto ToTransactionDto(CreditCardTransaction transaction, HouseholdMember currentMember)
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
            transaction.UniverseId,
            transaction.Universe?.Name,
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

    private static CreditCardStatementDto ToStatementDto(CreditCardStatement statement, HouseholdMember currentMember)
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

    private static FinanceCategoryDto ToCategoryDto(
        FinanceCategory category,
        HouseholdMember currentMember,
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

    private async Task<Guid> ResolveHouseholdIdAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return await ResolveSuperAdminHouseholdIdAsync(cancellationToken);
        }

        var memberships = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.UserId == userContext.UserId && member.IsActive)
            .Select(member => member.HouseholdId)
            .ToArrayAsync(cancellationToken);

        if (memberships.Length == 0)
        {
            throw new ForbiddenException("Usuário sem casa vinculada.");
        }

        if (userContext.HouseholdId is null)
        {
            if (memberships.Length == 1)
            {
                return memberships[0];
            }

            throw new ValidationException("Informe X-Household-Id para escolher a casa.");
        }

        if (!memberships.Contains(userContext.HouseholdId.Value))
        {
            throw new ForbiddenException("Você não tem acesso a esta casa.");
        }

        return userContext.HouseholdId.Value;
    }

    private async Task<HouseholdMember> ResolveCurrentMemberAsync(Guid householdId, CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return new HouseholdMember
            {
                HouseholdId = householdId,
                UserId = userContext.UserId,
                Role = HouseholdRole.Member
            };
        }

        return await db.HouseholdMembers
            .Include(member => member.User)
            .FirstOrDefaultAsync(member =>
                member.HouseholdId == householdId &&
                member.UserId == userContext.UserId &&
                member.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a esta casa.");
    }

    private async Task<HouseholdMember> ResolveCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var householdId = await ResolveHouseholdIdAsync(cancellationToken);
        return await ResolveCurrentMemberAsync(householdId, cancellationToken);
    }

    private async Task<Guid> ResolveSuperAdminHouseholdIdAsync(CancellationToken cancellationToken)
    {
        if (userContext.HouseholdId is null)
        {
            var householdIds = await db.Households
                .AsNoTracking()
                .OrderBy(household => household.Name)
                .Select(household => household.Id)
                .Take(2)
                .ToArrayAsync(cancellationToken);

            return householdIds.Length switch
            {
                0 => throw new NotFoundException("Casa não encontrada."),
                1 => householdIds[0],
                _ => throw new ValidationException("Informe X-Household-Id para escolher a casa.")
            };
        }

        var exists = await db.Households
            .AsNoTracking()
            .AnyAsync(household => household.Id == userContext.HouseholdId.Value, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Casa não encontrada.");
        }

        return userContext.HouseholdId.Value;
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

    private static bool IsContentManager(HouseholdMember member)
    {
        return member.Role is HouseholdRole.Owner or HouseholdRole.Admin;
    }

    private static bool CanManageEntity(HouseholdMember member, Guid? createdByMemberId)
    {
        return IsContentManager(member) || createdByMemberId == member.Id;
    }

    private static void EnsureCanManageEntity(HouseholdMember member, Guid? createdByMemberId, string message)
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

    private sealed record ProjectUniverseSelection(
        Guid? UniverseId,
        Universe? Universe,
        Guid? ProjectId,
        Project? Project);
}
