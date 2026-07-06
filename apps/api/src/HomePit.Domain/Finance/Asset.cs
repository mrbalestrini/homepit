using HomePit.Domain.Common;
using HomePit.Domain.Households;

namespace HomePit.Domain.Finance;

public sealed class Asset : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public HouseholdMember? CreatedByMember { get; set; }

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
