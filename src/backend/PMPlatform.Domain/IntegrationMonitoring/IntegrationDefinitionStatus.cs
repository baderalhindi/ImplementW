namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// Whether an integration is monitored (ERD §6 row 49). This is activation, never health: a DISABLED integration is not
/// an unavailable one (BR-INT-008), and disabling it changes nothing in the adapter that calls the provider.
/// </summary>
public enum IntegrationDefinitionStatus
{
    /// <summary>Monitored: its invocations are recorded, its health evaluated and its alerts raised.</summary>
    Active = 1,

    /// <summary>Not monitored for now: telemetry is still recorded, but no alert is raised and FG-05 runs none of its operations.</summary>
    Disabled = 2,

    /// <summary>No longer in use; kept with its history.</summary>
    Retired = 3,
}
