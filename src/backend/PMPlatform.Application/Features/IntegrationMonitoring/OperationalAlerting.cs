using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// Raises an operational alert for investigation (FG-05 §10): one live alert per instance and condition, so a provider outage is one
/// alert, not one per failed call (BR-INT-036); none for a disabled integration (BR-INT-008). Raising an alert changes nothing outside
/// <c>integration_monitoring</c> — in particular no source record, and nothing is repaired (BR-INT-006). Called with the instance locked.
/// </summary>
internal sealed class OperationalAlerting(IIntegrationMonitoringRepository repository, IAuditTrail audit, TimeProvider timeProvider)
{
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
        return alert;
    }

    /// <summary>Fixed per condition until AHDA sets its own levels (TBC-INT-17): an integration that is down is critical, the rest warnings.</summary>
    public static OperationalAlertSeverity SeverityOf(OperationalAlertType alertType) => alertType switch
    {
        OperationalAlertType.Availability => OperationalAlertSeverity.Critical,
        OperationalAlertType.Freshness or OperationalAlertType.ReconciliationMismatch or OperationalAlertType.DeadLetter or OperationalAlertType.DeliveryRate
            => OperationalAlertSeverity.Warning,
        _ => throw new ArgumentOutOfRangeException(nameof(alertType), alertType, "Unknown alert type."),
    };
}
