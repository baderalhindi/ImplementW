namespace PMPlatform.Infrastructure.Notifications;

/// <summary>Configuration section <c>Notifications:Worker</c>: how often the WF-15 pass runs and how much it takes on.</summary>
internal sealed class NotificationWorkerOptions
{
    public const string Section = "Notifications:Worker";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    public int BatchSize { get; set; } = 50;
}
