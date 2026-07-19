using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Projects;

namespace OrganizaClub.Domain.Finance;

public sealed class CreditCardTransaction : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid CreditCardAccountId { get; set; }
    public CreditCardAccount? CreditCardAccount { get; set; }

    public Guid? CreditCardStatementId { get; set; }
    public CreditCardStatement? CreditCardStatement { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public Guid? CoreId { get; set; }
    public Core? Core { get; set; }

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
