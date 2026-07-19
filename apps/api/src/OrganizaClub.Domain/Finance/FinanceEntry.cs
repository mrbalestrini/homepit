using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Projects;

namespace OrganizaClub.Domain.Finance;

public sealed class FinanceEntry : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid FinancePeriodId { get; set; }
    public FinancePeriod? FinancePeriod { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public Guid? RecurringTemplateId { get; set; }
    public FinanceRecurringTemplate? RecurringTemplate { get; set; }

    public Guid? CreditCardStatementId { get; set; }
    public CreditCardStatement? CreditCardStatement { get; set; }

    public Guid? CoreId { get; set; }
    public Core? Core { get; set; }

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
