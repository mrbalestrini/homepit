using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Plans;

public sealed class PlanDefinition : AuditableEntity
{
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string CurrencyCode { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int MaxOwnedSpaces { get; set; }
    public int MaxCores { get; set; }
    public int MaxProjects { get; set; }
    public int? MaxInvitedMembers { get; set; }
    public int MaxOriginalImages { get; set; }
    public bool ShowInCatalog { get; set; }
    public bool IsPopular { get; set; }
    public int SortOrder { get; set; }

    public ICollection<UserSubscription> Subscriptions { get; } = new List<UserSubscription>();
}
