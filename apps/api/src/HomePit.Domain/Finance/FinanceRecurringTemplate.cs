using HomePit.Domain.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;

namespace HomePit.Domain.Finance;

public sealed class FinanceRecurringTemplate : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public Guid? UniverseId { get; set; }
    public Universe? Universe { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? CategoryId { get; set; }
    public FinanceCategory? Category { get; set; }

    public required string Title { get; set; }
    public string? Notes { get; set; }
    public FinanceEntryType Type { get; set; }
    public decimal DefaultAmount { get; set; }
    public FinanceRecurrence Recurrence { get; set; }
    public int? DayOfMonth { get; set; }
    public int? MonthOfYear { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<FinanceEntry> Entries { get; } = new List<FinanceEntry>();
}
