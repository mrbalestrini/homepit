namespace OrganizaClub.Infrastructure.Notifications;

public sealed class EvolutionOptions
{
    public const string SectionName = "EvolutionApi";

    public string BaseUrl { get; set; } = "http://evolution-api:8080";
    public string InstanceName { get; set; } = "organiza-club";
    public string ApiKey { get; set; } = "";
    public string SendTextPathTemplate { get; set; } = "/message/sendText/{instance}";
}
