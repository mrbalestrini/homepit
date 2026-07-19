using OrganizaClub.Domain.Plans;

namespace OrganizaClub.Application.Plans;

public sealed record PlanDefinitionDto(
    Guid Id,
    string Slug,
    string Name,
    string CurrencyCode,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int MaxOwnedSpaces,
    int MaxCores,
    int MaxProjects,
    int? MaxInvitedMembers,
    int MaxOriginalImages,
    bool ShowInCatalog,
    bool IsPopular,
    string ImagePolicyDescription);

public sealed record UpdatePlanDefinitionRequest(
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int MaxOwnedSpaces,
    int MaxCores,
    int MaxProjects,
    int? MaxInvitedMembers,
    int MaxOriginalImages,
    bool ShowInCatalog,
    bool IsPopular);

public sealed record UserSubscriptionDto(
    Guid Id,
    Guid UserId,
    string UserDisplayName,
    string UserEmail,
    Guid PlanDefinitionId,
    string PlanSlug,
    string PlanName,
    BillingCycle BillingCycle,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal AmountPaid,
    string CurrencyCode,
    UserSubscriptionStatus Status,
    string? AdminNote);

public sealed record CreateUserSubscriptionRequest(
    Guid UserId,
    Guid PlanDefinitionId,
    BillingCycle BillingCycle,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal AmountPaid,
    string CurrencyCode,
    UserSubscriptionStatus Status,
    string? AdminNote);

public sealed record UpdateUserSubscriptionRequest(
    Guid UserId,
    Guid PlanDefinitionId,
    BillingCycle BillingCycle,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal AmountPaid,
    string CurrencyCode,
    UserSubscriptionStatus Status,
    string? AdminNote);

public sealed record PlanUsageSummaryDto(
    int OwnedSpaceCount,
    int CoreCount,
    int ProjectCount,
    int InvitedMemberCount,
    int ManagedOriginalImageCount);

public sealed record CurrentUserPlanSummaryDto(
    PlanDefinitionDto Plan,
    UserSubscriptionDto? ActiveSubscription,
    PlanUsageSummaryDto Usage);

public sealed record PlanCreationItemDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    Guid SpaceId,
    string SpaceName,
    bool CanDelete,
    Guid? CoreId,
    string? CoreName);

public sealed record AdminUserCommercialSummaryDto(
    string EffectivePlanSlug,
    string EffectivePlanName,
    Guid? ActiveSubscriptionId,
    BillingCycle? ActiveSubscriptionBillingCycle,
    DateTimeOffset? ActiveSubscriptionStartsAt,
    DateTimeOffset? ActiveSubscriptionEndsAt,
    decimal? ActiveSubscriptionAmountPaid,
    string? ActiveSubscriptionCurrencyCode,
    UserSubscriptionStatus? ActiveSubscriptionStatus);
