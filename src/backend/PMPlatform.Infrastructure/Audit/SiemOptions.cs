namespace PMPlatform.Infrastructure.Audit;

/// <summary>Configuration section <c>Audit:Siem</c>: how and how often the platform reaches the SIEM (TASK-033).</summary>
internal sealed class SiemOptions
{
    public const string Section = "Audit:Siem";

    /// <summary>The SIEM only over HTTPS. Cleared only by the tests' in-process SIEM.</summary>
    public bool RequireHttps { get; set; } = true;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How long the forwarder waits after a pass that found nothing more to send.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 100;
}
