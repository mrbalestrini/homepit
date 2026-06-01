namespace HomePit.Infrastructure.Notifications;

public sealed class DailyDigestWorkerOptions
{
    public const string SectionName = "Notifications";

    public bool DailyDigestEnabled { get; set; } = true;
    public int PollIntervalMinutes { get; set; } = 5;
}
