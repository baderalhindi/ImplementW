namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts.Events;

/// <summary>
/// The audit event types IntegrationMonitoring produces (TASK-075; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. Class INTEGRATION, so they are FG-06's integration audit (ADM-053), except the configuration changes. Attempts,
/// runs and health are operational telemetry in the module's own tables, not audit (BR-INT-052).
/// </summary>
public static class IntegrationMonitoringAuditEvents
{
    /// <summary>INTEGRATION, by svc.integration-monitoring: a condition was raised for investigation.</summary>
    public const string OperationalAlertRaised = "IntegrationMonitoring.OperationalAlertRaised";

    /// <summary>INTEGRATION: an operator acknowledged an alert.</summary>
    public const string AlertAcknowledged = "IntegrationMonitoring.AlertAcknowledged";

    /// <summary>INTEGRATION: an operator resolved an alert.</summary>
    public const string AlertResolved = "IntegrationMonitoring.AlertResolved";

    /// <summary>INTEGRATION: an operator dismissed an alert.</summary>
    public const string AlertDismissed = "IntegrationMonitoring.AlertDismissed";

    /// <summary>INTEGRATION: an operator retried a lineage FG-05 runs (FG-05 EVT-INT-019).</summary>
    public const string InvocationRetried = "IntegrationMonitoring.InvocationRetried";

    /// <summary>INTEGRATION: an operator requested a reconciliation.</summary>
    public const string ReconciliationRequested = "IntegrationMonitoring.ReconciliationRequested";

    /// <summary>INTEGRATION, by svc.integration-monitoring: a reconciliation attempt ended, with its counts (EVT-INT-037/038); FAILED when the source could not be read.</summary>
    public const string ReconciliationCompleted = "IntegrationMonitoring.ReconciliationCompleted";

    /// <summary>CONFIGURATION_CHANGE: an integration's monitoring was enabled (MOD-101).</summary>
    public const string IntegrationEnabled = "IntegrationMonitoring.IntegrationEnabled";

    /// <summary>CONFIGURATION_CHANGE: an integration's monitoring was disabled (MOD-101).</summary>
    public const string IntegrationDisabled = "IntegrationMonitoring.IntegrationDisabled";
}
