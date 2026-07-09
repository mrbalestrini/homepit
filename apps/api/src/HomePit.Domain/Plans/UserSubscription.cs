using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Plans;

public sealed class UserSubscription : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public Guid PlanDefinitionId { get; set; }
    public PlanDefinition? PlanDefinition { get; set; }

    public BillingCycle BillingCycle { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public decimal AmountPaid { get; set; }
    public required string CurrencyCode { get; set; }
    public UserSubscriptionStatus Status { get; set; } = UserSubscriptionStatus.Scheduled;
    public string? AdminNote { get; set; }
}
