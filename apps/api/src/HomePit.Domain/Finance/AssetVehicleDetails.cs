namespace HomePit.Domain.Finance;

public sealed class AssetVehicleDetails
{
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? YearModel { get; set; }
    public string? Renavam { get; set; }
}
