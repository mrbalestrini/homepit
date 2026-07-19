using OrganizaClub.Domain.Finance;

namespace OrganizaClub.Application.Finance;

public sealed record GenerateFinancePeriodRequest(string Mode);

public sealed record CreateFinanceCategoryRequest(string Name);

public sealed record UpdateFinanceCategoryRequest(string Name);

public sealed record FinanceCategoryDto(
    Guid Id,
    string Name,
    bool IsDefault,
    int SortOrder,
    Guid? CreatedByMemberId,
    int UsageCount,
    bool CanEdit,
    bool CanDelete);

public sealed record FinancePeriodListItemDto(
    Guid Id,
    int Year,
    int Month,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal CashBalance,
    int EntryCount);

public sealed record FinancePeriodSummaryDto(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal CashBalance,
    decimal AnalyticalExpenseTotal,
    int VerifiedEntries,
    int PendingVerificationEntries,
    int CardPurchaseCount);

public sealed record FinancePeriodDetailDto(
    Guid? Id,
    int Year,
    int Month,
    bool Exists,
    FinancePeriodSummaryDto Summary,
    IReadOnlyCollection<FinanceEntryDto> Entries,
    IReadOnlyCollection<CreditCardTransactionDto> CardTransactions,
    IReadOnlyCollection<CreditCardStatementDto> Statements);

public sealed record CreateFinanceRecurringTemplateRequest(
    string Title,
    string? Notes,
    FinanceEntryType Type,
    decimal DefaultAmount,
    FinanceRecurrence Recurrence,
    int? DayOfMonth,
    int? MonthOfYear,
    bool IsActive,
    Guid? CategoryId,
    Guid? CoreId,
    Guid? ProjectId);

public sealed record UpdateFinanceRecurringTemplateRequest(
    string Title,
    string? Notes,
    FinanceEntryType Type,
    decimal DefaultAmount,
    FinanceRecurrence Recurrence,
    int? DayOfMonth,
    int? MonthOfYear,
    bool IsActive,
    Guid? CategoryId,
    Guid? CoreId,
    Guid? ProjectId);

public sealed record FinanceRecurringTemplateDto(
    Guid Id,
    string Title,
    string? Notes,
    FinanceEntryType Type,
    decimal DefaultAmount,
    FinanceRecurrence Recurrence,
    int? DayOfMonth,
    int? MonthOfYear,
    bool IsActive,
    Guid? CategoryId,
    string? CategoryName,
    Guid? CoreId,
    string? CoreName,
    Guid? ProjectId,
    string? ProjectName,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);

public sealed record CreateFinanceEntryRequest(
    int Year,
    int Month,
    string Title,
    string? Notes,
    decimal Amount,
    FinanceEntryType Type,
    bool Verified,
    DateOnly ReferenceDate,
    Guid? RecurringTemplateId,
    Guid? CategoryId,
    Guid? CoreId,
    Guid? ProjectId);

public sealed record UpdateFinanceEntryRequest(
    int Year,
    int Month,
    string Title,
    string? Notes,
    decimal Amount,
    FinanceEntryType Type,
    bool Verified,
    DateOnly ReferenceDate,
    Guid? RecurringTemplateId,
    Guid? CategoryId,
    Guid? CoreId,
    Guid? ProjectId);

public sealed record FinanceEntryDto(
    Guid Id,
    Guid PeriodId,
    int Year,
    int Month,
    string Title,
    string? Notes,
    decimal Amount,
    FinanceEntryType Type,
    bool Verified,
    DateOnly ReferenceDate,
    FinanceEntryOrigin Origin,
    Guid? RecurringTemplateId,
    Guid? CreditCardStatementId,
    Guid? CategoryId,
    string? CategoryName,
    Guid? CoreId,
    string? CoreName,
    Guid? ProjectId,
    string? ProjectName,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);

public sealed record AssetPropertyDetailsRequest(
    string? RegistryNumber,
    string? PropertyInscription,
    decimal? PrivateAreaSquareMeters,
    DateOnly? DebtCheckOn);

public sealed record AssetVehicleDetailsRequest(
    string? Brand,
    string? Model,
    string? YearModel,
    string? Renavam);

public sealed record CreateAssetRequest(
    string Title,
    AssetType Type,
    decimal? CurrentValue,
    decimal? RemainingDebt,
    bool IsPaidOff,
    string? Notes,
    AssetPropertyDetailsRequest? PropertyDetails,
    AssetVehicleDetailsRequest? VehicleDetails);

public sealed record UpdateAssetRequest(
    string Title,
    AssetType Type,
    decimal? CurrentValue,
    decimal? RemainingDebt,
    bool IsPaidOff,
    string? Notes,
    AssetPropertyDetailsRequest? PropertyDetails,
    AssetVehicleDetailsRequest? VehicleDetails);

public sealed record AssetPropertyDetailsDto(
    string? RegistryNumber,
    string? PropertyInscription,
    decimal? PrivateAreaSquareMeters,
    DateOnly? DebtCheckOn);

public sealed record AssetVehicleDetailsDto(
    string? Brand,
    string? Model,
    string? YearModel,
    string? Renavam);

public sealed record AssetDto(
    Guid Id,
    string Title,
    AssetType Type,
    decimal? CurrentValue,
    decimal? RemainingDebt,
    bool IsPaidOff,
    string? Notes,
    AssetPropertyDetailsDto? PropertyDetails,
    AssetVehicleDetailsDto? VehicleDetails,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);

public sealed record CreateAssetValuationRequest(
    int ReferenceYear,
    string Label,
    decimal Amount,
    string? Notes);

public sealed record UpdateAssetValuationRequest(
    int ReferenceYear,
    string Label,
    decimal Amount,
    string? Notes);

public sealed record AssetValuationDto(
    Guid Id,
    Guid AssetId,
    int ReferenceYear,
    string Label,
    decimal Amount,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);

public sealed record CreateCreditCardAccountRequest(
    string Name,
    string? Brand,
    string? LastFourDigits,
    int ClosingDay,
    int DueDay,
    string? Notes,
    bool IsActive);

public sealed record UpdateCreditCardAccountRequest(
    string Name,
    string? Brand,
    string? LastFourDigits,
    int ClosingDay,
    int DueDay,
    string? Notes,
    bool IsActive);

public sealed record CreditCardAccountDto(
    Guid Id,
    string Name,
    string? Brand,
    string? LastFourDigits,
    int ClosingDay,
    int DueDay,
    string? Notes,
    bool IsActive,
    int OpenTransactionCount,
    decimal OpenTransactionTotal,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);

public sealed record CreateCreditCardTransactionRequest(
    string Title,
    string? Merchant,
    decimal Amount,
    DateOnly PurchasedOn,
    string? Notes,
    Guid? CategoryId,
    Guid? CoreId,
    Guid? ProjectId,
    string? ExternalSource,
    string? ExternalReference,
    DateTimeOffset? ImportedAt);

public sealed record ImportCreditCardTransactionItem(
    string Title,
    string? Merchant,
    decimal Amount,
    DateOnly PurchasedOn,
    string? Notes,
    string? CategoryName,
    string? CoreName,
    string? ProjectName,
    string? ExternalSource,
    string? ExternalReference,
    DateTimeOffset? ImportedAt);

public sealed record ImportCreditCardTransactionsRequest(
    IReadOnlyCollection<ImportCreditCardTransactionItem> Transactions);

public sealed record UpdateCreditCardTransactionRequest(
    string Title,
    string? Merchant,
    decimal Amount,
    DateOnly PurchasedOn,
    string? Notes,
    Guid? CategoryId,
    Guid? CoreId,
    Guid? ProjectId,
    string? ExternalSource,
    string? ExternalReference,
    DateTimeOffset? ImportedAt);

public sealed record CreditCardTransactionDto(
    Guid Id,
    Guid CreditCardAccountId,
    string CreditCardAccountName,
    Guid? CreditCardStatementId,
    string Title,
    string? Merchant,
    decimal Amount,
    DateOnly PurchasedOn,
    string? Notes,
    Guid? CategoryId,
    string? CategoryName,
    Guid? CoreId,
    string? CoreName,
    Guid? ProjectId,
    string? ProjectName,
    string? ExternalSource,
    string? ExternalReference,
    DateTimeOffset? ImportedAt,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);

public sealed record ImportCreditCardTransactionsResponse(
    int TotalCount,
    decimal TotalAmount,
    int CreatedCategoryCount,
    IReadOnlyCollection<CreditCardTransactionDto> CreatedTransactions);

public sealed record CreateCreditCardStatementRequest(
    DateOnly ClosingDate,
    DateOnly DueDate,
    string? Notes,
    Guid[] TransactionIds,
    string? ExternalSource,
    string? ExternalReference,
    DateTimeOffset? ImportedAt);

public sealed record UpdateCreditCardStatementRequest(
    DateOnly ClosingDate,
    DateOnly DueDate,
    string? Notes,
    Guid[] TransactionIds,
    string? ExternalSource,
    string? ExternalReference,
    DateTimeOffset? ImportedAt);

public sealed record CreditCardStatementDto(
    Guid Id,
    Guid CreditCardAccountId,
    string CreditCardAccountName,
    DateOnly ClosingDate,
    DateOnly DueDate,
    decimal TotalAmount,
    string? Notes,
    int TransactionCount,
    Guid? FinanceEntryId,
    string? ExternalSource,
    string? ExternalReference,
    DateTimeOffset? ImportedAt,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);
