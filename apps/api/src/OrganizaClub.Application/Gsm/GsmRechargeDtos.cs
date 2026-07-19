namespace OrganizaClub.Application.Gsm;

public sealed record CreateGsmRechargeRequest(
    DateOnly RechargedOn,
    decimal? Amount,
    string? Note);

public sealed record UpdateGsmRechargeRequest(
    DateOnly RechargedOn,
    decimal? Amount,
    string? Note);

public sealed record GsmRechargeDto(
    Guid Id,
    Guid GsmNumberId,
    DateOnly RechargedOn,
    decimal? Amount,
    string? Note,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);
