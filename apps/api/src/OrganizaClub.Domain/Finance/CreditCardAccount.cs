using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Finance;

public sealed class CreditCardAccount : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public required string Name { get; set; }
    public string? Brand { get; set; }
    public string? LastFourDigits { get; set; }
    public int ClosingDay { get; set; }
    public int DueDay { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CreditCardTransaction> Transactions { get; } = new List<CreditCardTransaction>();
    public ICollection<CreditCardStatement> Statements { get; } = new List<CreditCardStatement>();
}
