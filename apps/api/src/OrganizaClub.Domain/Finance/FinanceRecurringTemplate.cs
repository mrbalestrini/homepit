using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Projects;

namespace OrganizaClub.Domain.Finance;

public sealed class FinanceRecurringTemplate : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public Guid? CoreId { get; set; }
    public Core? Core { get; set; }

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
