using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Gsm;

public sealed class GsmNumber : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public required string Title { get; set; }
    public required string NormalizedNumber { get; set; }
    public string? Description { get; set; }
    public GsmNumberPlan Plan { get; set; } = GsmNumberPlan.PrePago;
    public decimal? MonthlyCost { get; set; }
    public int? DaysWithoutRecharge { get; set; }
    public DateOnly AcquiredOn { get; set; }
    public DateOnly? LastRechargeOn { get; set; }
    public GsmNumberStatus Status { get; set; } = GsmNumberStatus.Ativo;
    public ICollection<GsmRecharge> Recharges { get; } = new List<GsmRecharge>();
}
