using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Gsm;

public sealed class GsmRecharge : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid GsmNumberId { get; set; }
    public GsmNumber? GsmNumber { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public DateOnly RechargedOn { get; set; }
    public decimal? Amount { get; set; }
    public string? Note { get; set; }
}
