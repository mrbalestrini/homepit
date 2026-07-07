using HomePit.Domain.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;

namespace HomePit.Domain.Finance;

public sealed class CreditCardTransaction : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid CreditCardAccountId { get; set; }
    public CreditCardAccount? CreditCardAccount { get; set; }

    public Guid? CreditCardStatementId { get; set; }
    public CreditCardStatement? CreditCardStatement { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public Guid? UniverseId { get; set; }
    public Universe? Universe { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? CategoryId { get; set; }
    public FinanceCategory? Category { get; set; }

    public required string Title { get; set; }
    public string? Merchant { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PurchasedOn { get; set; }
    public string? Notes { get; set; }
    public string? ExternalSource { get; set; }
    public string? ExternalReference { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
}
