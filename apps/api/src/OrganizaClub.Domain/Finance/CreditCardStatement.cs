using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Finance;

public sealed class CreditCardStatement : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid CreditCardAccountId { get; set; }
    public CreditCardAccount? CreditCardAccount { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

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
