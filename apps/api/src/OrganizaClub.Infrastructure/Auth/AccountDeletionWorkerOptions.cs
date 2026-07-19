namespace OrganizaClub.Infrastructure.Auth;

public sealed class AccountDeletionWorkerOptions
{
    public const string SectionName = "AccountLifecycle";

    public bool Enabled { get; set; } = true;
    public int PollIntervalMinutes { get; set; } = 60;
}
