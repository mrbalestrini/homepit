using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Gsm;

public sealed class GsmNumber : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

    public required string Title { get; set; }
    public required string NormalizedNumber { get; set; }
    public string? Description { get; set; }
    public DateOnly AcquiredOn { get; set; }
    public DateOnly? LastRechargeOn { get; set; }
    public GsmNumberStatus Status { get; set; } = GsmNumberStatus.Ativo;
}
