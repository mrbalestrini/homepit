using OrganizaClub.Domain.Gsm;

namespace OrganizaClub.Application.Gsm;

public sealed record CreateGsmNumberRequest(
    string Title,
    string Number,
    string? Description,
    GsmNumberPlan Plan,
    decimal? MonthlyCost,
    int? DaysWithoutRecharge,
    DateOnly AcquiredOn,
    GsmNumberStatus Status);

public sealed record UpdateGsmNumberRequest(
    string Title,
    string Number,
    string? Description,
    GsmNumberPlan Plan,
    decimal? MonthlyCost,
    int? DaysWithoutRecharge,
    DateOnly AcquiredOn,
    GsmNumberStatus Status);

public sealed record GsmNumberDto(
    Guid Id,
    string Title,
    string Number,
    string? Description,
    GsmNumberPlan Plan,
    decimal? MonthlyCost,
    int? DaysWithoutRecharge,
    DateOnly AcquiredOn,
    DateOnly? LastRechargeOn,
    GsmNumberStatus Status,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);
