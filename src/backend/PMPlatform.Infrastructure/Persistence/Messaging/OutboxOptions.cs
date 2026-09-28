namespace PMPlatform.Infrastructure.Persistence.Messaging;

/// <summary>Configuration section <c>Outbox</c>: how often and how much the dispatcher delivers (event-conventions EV-6).</summary>
internal sealed class OutboxOptions
{
    public const string Section = "Outbox";

    /// <summary>How long the dispatcher waits after a pass that found nothing more to deliver.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 50;

    /// <summary>The wait after a first failed attempt; each later failure doubles it (EV-6: exponential back-off).</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);
}
