namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// Whether the integration has succeeded recently enough (FG-05 §5.4 Data Freshness), against the window its policy
/// configures. Without a configured window it stays <see cref="Unknown"/>: no threshold is invented (BR-INT-016).
/// </summary>
public enum IntegrationFreshness
{
    /// <summary>The last success is within the configured window.</summary>
    Fresh = 1,

    /// <summary>No success within the configured window.</summary>
    Stale = 2,

    /// <summary>No window is configured, or nothing has succeeded yet.</summary>
    Unknown = 3,
}
