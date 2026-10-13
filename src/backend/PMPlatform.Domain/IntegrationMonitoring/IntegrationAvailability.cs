namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// Whether the provider answered the last technical attempt (FG-05 §5.1 Endpoint Availability). Separate from
/// <see cref="IntegrationFreshness"/> (BR-INT-013): a reachable source can still be stale.
/// </summary>
public enum IntegrationAvailability
{
    /// <summary>The last attempt reached the provider and it answered, whatever the business answer was.</summary>
    Up = 1,

    /// <summary>The last attempt failed technically: no connection, a timeout, a provider error.</summary>
    Down = 2,

    /// <summary>No attempt has been observed yet.</summary>
    Unknown = 3,
}
