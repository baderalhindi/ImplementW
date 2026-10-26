using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// A condition raised for investigation (Blueprint §17.1, FG-05 §10): availability, freshness, a reconciliation mismatch, a dead
/// letter, a delivery rate, a queue backlog. One alert stays open per instance and condition, so a provider outage is one alert, not one per call
/// (BR-INT-036). Its lifecycle is the operator's; it neither drives nor follows the integration's health. Delete policy: RETAIN.
/// </summary>
public sealed class OperationalAlert : AuditedEntity
{
    public Guid IntegrationInstanceId { get; set; }

    public Guid? SyncRunId { get; set; }

    public Guid? InvocationId { get; set; }

    public OperationalAlertType AlertType { get; set; }

    public OperationalAlertSeverity Severity { get; set; }

    public DateTimeOffset RaisedAt { get; set; }

    public OperationalAlertStatus Status { get; set; }

    public Guid? AcknowledgedByUserId { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The operator's note on resolving or dismissing, as entered (ADR-012).</summary>
    public NarrativeText? ResolutionNote { get; set; }

    /// <summary>What was found, in counts, codes and ids; never a personal or secret value.</summary>
    public string? Detail { get; set; }
}
