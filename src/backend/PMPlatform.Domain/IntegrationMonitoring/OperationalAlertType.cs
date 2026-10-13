namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>The condition an operational alert reports (FG-05 §10.1).</summary>
public enum OperationalAlertType
{
    /// <summary>The integration's availability went DOWN.</summary>
    Availability = 1,

    /// <summary>The integration's freshness went STALE.</summary>
    Freshness = 2,

    /// <summary>A reconciliation found the source and the platform disagree. Nothing was repaired (BR-INT-006).</summary>
    ReconciliationMismatch = 3,

    /// <summary>An invocation lineage was dead-lettered.</summary>
    DeadLetter = 4,

    /// <summary>The delivery rate fell below the configured minimum (ADR-004: the SMS provider's delivery rate).</summary>
    DeliveryRate = 5,
}
