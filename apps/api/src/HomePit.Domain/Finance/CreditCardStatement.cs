using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Finance;

public sealed class CreditCardStatement : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid CreditCardAccountId { get; set; }
    public CreditCardAccount? CreditCardAccount { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public DateOnly ClosingDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string? ExternalSource { get; set; }
    public string? ExternalReference { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }

    public FinanceEntry? FinanceEntry { get; set; }
    public ICollection<CreditCardTransaction> Transactions { get; } = new List<CreditCardTransaction>();
}
