using PMPlatform.Application.Common.Events;

namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts.Events;

/// <summary>
/// <c>IntegrationMonitoring.OperationalAlertRaised</c> (event-conventions §3 row 7; FG-05 EVT-INT-042 "Operational Alert Opened"): the
/// NOTIFICATION_INTENT a raised alert publishes to WF-15 (edge E-U4), staged in the transaction that raises it, so FG-05 implements no
/// second notification engine (FG-05 §10.3). WF-15 resolves the recipients from the family's roles in the NOTIFICATION_ROUTING version
/// in force — the configured recipient — and its channels and templates. Its idempotency key is the alert's id: one alert, one intent.
/// </summary>
public sealed record OperationalAlertRaised : EventEnvelope<NotificationIntentData>
{
    public const string Type = "IntegrationMonitoring.OperationalAlertRaised";

    public const int CurrentSchemaVersion = 1;

    /// <summary>The FG-04 NOTIFICATION_ROUTING event family that decides its channels and recipient roles (ADR-004; TBC-INT-17).</summary>
    public const string EventFamilyCode = "INTEGRATION_OPERATIONAL_ALERT";
}
