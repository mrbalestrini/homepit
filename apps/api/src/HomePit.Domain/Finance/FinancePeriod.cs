using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Finance;

public sealed class FinancePeriod : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    public ICollection<FinanceEntry> Entries { get; } = new List<FinanceEntry>();
}
