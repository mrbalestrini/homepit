using HomePit.Domain.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;

namespace HomePit.Domain.Finance;

public sealed class FinanceEntry : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid FinancePeriodId { get; set; }
    public FinancePeriod? FinancePeriod { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public Guid? RecurringTemplateId { get; set; }
    public FinanceRecurringTemplate? RecurringTemplate { get; set; }

    public Guid? CreditCardStatementId { get; set; }
    public CreditCardStatement? CreditCardStatement { get; set; }

    public Guid? UniverseId { get; set; }
    public Universe? Universe { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? CategoryId { get; set; }
    public FinanceCategory? Category { get; set; }

    public required string Title { get; set; }
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
    public FinanceEntryType Type { get; set; }
    public bool Verified { get; set; }
    public DateOnly ReferenceDate { get; set; }
    public FinanceEntryOrigin Origin { get; set; } = FinanceEntryOrigin.Manual;
}
