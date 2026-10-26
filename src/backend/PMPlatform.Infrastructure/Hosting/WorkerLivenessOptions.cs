namespace PMPlatform.Infrastructure.Hosting;

/// <summary>When a background worker counts as stalled (TASK-091). Bound from <c>HealthChecks</c>.</summary>
public sealed class WorkerLivenessOptions
{
    public const string Section = "HealthChecks";

    /// <summary>
    /// How long one pass may run before the worker is reported stalled and the instance restarted. A slow pass is not a stalled one: the
    /// slowest pass today is a notification batch against an SMTP server that never answers, 50 items × the 30-second
    /// Notifications:Email:Timeout, about 25 minutes, and a server that answers slowly stretches each step further, so the default
    /// leaves a wide margin.
    /// </summary>
    public TimeSpan WorkerPassTimeout { get; set; } = TimeSpan.FromHours(2);
}
