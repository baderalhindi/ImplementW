namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// An alert's attention lifecycle (FG-05 §10.2), distinct from the integration's health: acknowledging an alert does not
/// claim the integration is healthy, and the integration recovering does not close the alert (BR-INT-037, BR-INT-038).
/// </summary>
public enum OperationalAlertStatus
{
    /// <summary>Raised and not yet looked at.</summary>
    Open = 1,

    /// <summary>An operator has taken it; the condition may still hold.</summary>
    Acknowledged = 2,

    /// <summary>Handled, with a note. Final.</summary>
    Resolved = 3,

    /// <summary>Not a real condition, with a note. Final.</summary>
    Dismissed = 4,
}
