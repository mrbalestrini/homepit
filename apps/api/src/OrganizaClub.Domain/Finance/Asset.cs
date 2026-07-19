using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Finance;

public sealed class Asset : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public required string Title { get; set; }
    public AssetType Type { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? RemainingDebt { get; set; }
    public bool IsPaidOff { get; set; }
    public string? Notes { get; set; }

    public AssetPropertyDetails? PropertyDetails { get; set; }
    public AssetVehicleDetails? VehicleDetails { get; set; }
    public ICollection<AssetValuation> Valuations { get; } = new List<AssetValuation>();
}
