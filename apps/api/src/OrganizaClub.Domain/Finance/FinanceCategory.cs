using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Finance;

public sealed class FinanceCategory : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public required string Name { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }

    public ICollection<FinanceRecurringTemplate> RecurringTemplates { get; } = new List<FinanceRecurringTemplate>();
    public ICollection<FinanceEntry> Entries { get; } = new List<FinanceEntry>();
    public ICollection<CreditCardTransaction> CreditCardTransactions { get; } = new List<CreditCardTransaction>();
}
