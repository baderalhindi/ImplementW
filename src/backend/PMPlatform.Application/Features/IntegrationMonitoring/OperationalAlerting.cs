using System.Text.Json;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// Raises an operational alert for investigation (FG-05 §10): one live alert per instance and condition, so a provider outage is one
/// alert, not one per failed call (BR-INT-036); none for a disabled integration (BR-INT-008). Raising an alert changes nothing outside
/// <c>integration_monitoring</c> — in particular no source record, and nothing is repaired (BR-INT-006). Called with the instance locked.
/// Each alert raised is published, in the same transaction, as a NotificationIntent WF-15 routes to the configured recipients (FG-05
/// §10.3), and logged once on a fixed line the environment's log-based metric counts for the on-call channel (TASK-092).
/// </summary>
internal sealed partial class OperationalAlerting(
    IIntegrationMonitoringRepository repository, IAuditTrail audit, IOutbox outbox, TimeProvider timeProvider, ILogger<OperationalAlerting> logger)
{
    /// <summary>The SPA route of ADM-046, where the integration's alerts are handled; the intent's deep link.</summary>
    public const string DeepLinkPrefix = "/admin/integration-detail-configuration/";

    public async Task<OperationalAlert?> RaiseAsync(
        IntegrationDefinition definition, IntegrationInstance instance, OperationalAlertType alertType, string detail,
        Guid? invocationId, Guid? syncRunId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);
        if (definition.Status != IntegrationDefinitionStatus.Active
            || await repository.FindLiveAlertAsync(instance.Id, alertType, cancellationToken).ConfigureAwait(false) is not null)
        {
            return null;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        OperationalAlert alert = new()
        {
            Id = Guid.CreateVersion7(now),
            IntegrationInstanceId = instance.Id,
            InvocationId = invocationId,
            SyncRunId = syncRunId,
            AlertType = alertType,
            Severity = SeverityOf(alertType),
            RaisedAt = now,
            Status = OperationalAlertStatus.Open,
            Detail = detail,
            CreatedAt = now,
            CreatedBy = IntegrationMonitoringServicePrincipal.Id,
            UpdatedAt = now,
            UpdatedBy = IntegrationMonitoringServicePrincipal.Id,
        };
        repository.Add(alert);
        audit.Stage(IntegrationMonitoringAudit.AlertRaised(alert, definition.Code));
        outbox.Stage(Intent(alert, definition, now));
        LogAlertRaised(logger, Code(alert.AlertType), Code(alert.Severity), definition.Code, alert.Id);
        return alert;
    }

    /// <summary>
    /// The alert's NotificationIntent (EV-10): platform-wide, so its scope names no project, department or entity and WF-15 reaches the
    /// family's role holders whose assignment carries no anchor. Codes only: the detail stays on ADM-046, behind FG-05's permissions.
    /// </summary>
    private static OperationalAlertRaised Intent(OperationalAlert alert, IntegrationDefinition definition, DateTimeOffset now)
    {
        string key = alert.Id.ToString();
        return new OperationalAlertRaised
        {
            EventId = Guid.CreateVersion7(now),
            EventType = OperationalAlertRaised.Type,
            SchemaVersion = OperationalAlertRaised.CurrentSchemaVersion,
            Kind = EventKind.NotificationIntent,
            MessageKey = EventMessageKey.Of(OperationalAlertRaised.Type, key),
            IdempotencyKey = key,
            OccurredAt = now,
            SourceModule = IntegrationMonitoringServicePrincipal.Module,
            // The flow's own (a telemetry delivery's, a lineage's); a pass that runs under none starts one, never the key (R-45).
            CorrelationId = CorrelationScope.Current ?? Guid.CreateVersion7(now),
            Actor = new EventActor(AuditActorType.Service, IntegrationMonitoringServicePrincipal.Id),
            Subject = new EventSubject(IntegrationMonitoringServicePrincipal.Module, nameof(OperationalAlert), alert.Id, null),
            Scope = new EventScope(null, null, null),
            Data = new NotificationIntentData(
                OperationalAlertRaised.EventFamilyCode,
                key,
                null,
                DeepLinkPrefix + definition.Id,
                [
                    new NotificationParameter("integrationCode", definition.Code),
                    new NotificationParameter("alertType", Code(alert.AlertType)),
                    new NotificationParameter("severity", Code(alert.Severity)),
                ],
                null),
        };
    }

    private static string Code<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    /// <summary>Fixed per condition until AHDA sets its own levels (TBC-INT-17): an integration that is down is critical, the rest warnings.</summary>
    public static OperationalAlertSeverity SeverityOf(OperationalAlertType alertType) => alertType switch
    {
        OperationalAlertType.Availability => OperationalAlertSeverity.Critical,
        OperationalAlertType.Freshness or OperationalAlertType.ReconciliationMismatch or OperationalAlertType.DeadLetter or OperationalAlertType.DeliveryRate
            or OperationalAlertType.QueueBacklog => OperationalAlertSeverity.Warning,
        _ => throw new ArgumentOutOfRangeException(nameof(alertType), alertType, "Unknown alert type."),
    };

    /// <summary>
    /// One line per alert raised, in a fixed shape: the environment's log-based metric (infra/terraform/monitoring) matches
    /// "Operational alert raised:" and reads the type, severity and integration from it (docs/operations/operational-dashboards.md).
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Operational alert raised: {AlertType} {Severity} on {Integration} (alert {AlertId}).")]
    private static partial void LogAlertRaised(ILogger logger, string alertType, string severity, string integration, Guid alertId);
}
