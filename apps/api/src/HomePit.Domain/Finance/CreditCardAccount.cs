using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Finance;

public sealed class CreditCardAccount : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

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
