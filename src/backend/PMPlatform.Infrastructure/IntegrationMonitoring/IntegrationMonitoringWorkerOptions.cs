namespace PMPlatform.Infrastructure.IntegrationMonitoring;

/// <summary>Configuration section <c>IntegrationMonitoring:Worker</c>: how often FG-05's pass runs (TASK-075).</summary>
internal sealed class IntegrationMonitoringWorkerOptions
{
    public const string Section = "IntegrationMonitoring:Worker";

    /// <summary>How long the pass waits after one that found no full batch of retries.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    public int BatchSize { get; set; } = 50;
}
