using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Gsm;

public sealed class GsmRecharge : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid GsmNumberId { get; set; }
    public GsmNumber? GsmNumber { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public DateOnly RechargedOn { get; set; }
    public decimal? Amount { get; set; }
    public string? Note { get; set; }
}
