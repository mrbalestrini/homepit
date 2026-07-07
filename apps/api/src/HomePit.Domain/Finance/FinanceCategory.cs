using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Finance;

public sealed class FinanceCategory : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public required string Name { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }

    public ICollection<FinanceRecurringTemplate> RecurringTemplates { get; } = new List<FinanceRecurringTemplate>();
    public ICollection<FinanceEntry> Entries { get; } = new List<FinanceEntry>();
    public ICollection<CreditCardTransaction> CreditCardTransactions { get; } = new List<CreditCardTransaction>();
}
