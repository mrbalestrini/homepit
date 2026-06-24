using HomePit.Domain.Gsm;

namespace HomePit.Application.Gsm;

public sealed record CreateGsmNumberRequest(
    string Title,
    string Number,
    string? Description,
    GsmNumberPlan Plan,
    decimal? MonthlyCost,
    DateOnly AcquiredOn,
    DateOnly? LastRechargeOn,
    GsmNumberStatus Status);

public sealed record UpdateGsmNumberRequest(
    string Title,
    string Number,
    string? Description,
    GsmNumberPlan Plan,
    decimal? MonthlyCost,
    DateOnly AcquiredOn,
    DateOnly? LastRechargeOn,
    GsmNumberStatus Status);

public sealed record GsmNumberDto(
    Guid Id,
    string Title,
    string Number,
    string? Description,
    GsmNumberPlan Plan,
    decimal? MonthlyCost,
    DateOnly AcquiredOn,
    DateOnly? LastRechargeOn,
    GsmNumberStatus Status,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);
