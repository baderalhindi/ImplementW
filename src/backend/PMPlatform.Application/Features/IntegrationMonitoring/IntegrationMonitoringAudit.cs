using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>The audit events of FG-05 (TASK-033 <c>IAuditTrail</c>): codes, counts and ids, never a payload or a personal value.</summary>
internal static class IntegrationMonitoringAudit
{
    private const string Module = IntegrationMonitoringServicePrincipal.Module;

    public static AuditEntry AlertRaised(OperationalAlert alert, string integrationCode) =>
        new(AuditEventClass.Integration, IntegrationMonitoringAuditEvents.OperationalAlertRaised, AuditOutcome.Success)
        {
            ActorUserId = IntegrationMonitoringServicePrincipal.Id,
            ActorType = AuditActorType.Service,
            Subject = new AuditSubject(Module, nameof(OperationalAlert), alert.Id),
            Attributes =
            [
                AuditAttribute.Of("integration_code", integrationCode),
                AuditAttribute.Of("alert_type", alert.AlertType),
                AuditAttribute.Of("severity", alert.Severity),
                AuditAttribute.Of("invocation_id", alert.InvocationId),
                AuditAttribute.Of("sync_run_id", alert.SyncRunId),
            ],
        };

    public static AuditEntry AlertHandled(string eventType, Guid actorId, OperationalAlert alert, OperationalAlertStatus before) =>
        new(AuditEventClass.Integration, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(OperationalAlert), alert.Id),
            Attributes =
            [
                AuditAttribute.Of("alert_type", alert.AlertType),
                AuditAttribute.Change("status", before, alert.Status)!,
            ],
        };

    public static AuditEntry InvocationRetried(Guid actorId, Invocation from, Invocation attempt, string integrationCode) =>
        new(AuditEventClass.Integration, IntegrationMonitoringAuditEvents.InvocationRetried, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(Invocation), attempt.Id),
            Attributes =
            [
                AuditAttribute.Of("integration_code", integrationCode),
                AuditAttribute.Of("operation_code", attempt.OperationCode),
                AuditAttribute.Of("retried_invocation_id", from.Id),
                AuditAttribute.Of("retried_status", from.Status),
                AuditAttribute.Of("attempt_no", attempt.AttemptNo),
                AuditAttribute.Of("lineage_correlation_id", attempt.CorrelationId),
            ],
        };

    public static AuditEntry ReconciliationRequested(Guid actorId, Invocation attempt, string integrationCode) =>
        new(AuditEventClass.Integration, IntegrationMonitoringAuditEvents.ReconciliationRequested, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(Invocation), attempt.Id),
            Attributes =
            [
                AuditAttribute.Of("integration_code", integrationCode),
                AuditAttribute.Of("operation_code", attempt.OperationCode),
            ],
        };

    public static AuditEntry ReconciliationCompleted(SyncRun run, Invocation attempt, string integrationCode) =>
        new(AuditEventClass.Integration, IntegrationMonitoringAuditEvents.ReconciliationCompleted,
            run.Status is SyncRunStatus.Succeeded ? AuditOutcome.Success : AuditOutcome.Failed)
        {
            ActorUserId = IntegrationMonitoringServicePrincipal.Id,
            ActorType = AuditActorType.Service,
            Subject = new AuditSubject(Module, nameof(SyncRun), run.Id),
            Attributes =
            [
                AuditAttribute.Of("integration_code", integrationCode),
                AuditAttribute.Of("status", run.Status),
                AuditAttribute.Of("attempt_no", attempt.AttemptNo),
                AuditAttribute.Of("records_read", run.RecordsRead),
                AuditAttribute.Of("records_applied", run.RecordsApplied),
                AuditAttribute.Of("records_rejected", run.RecordsRejected),
                AuditAttribute.Of("mismatch_count", run.MismatchCount),
            ],
        };

    public static AuditEntry ActivationChanged(string eventType, Guid actorId, IntegrationDefinition definition, IntegrationDefinitionStatus before) =>
        new(AuditEventClass.ConfigurationChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(IntegrationDefinition), definition.Id),
            Attributes =
            [
                AuditAttribute.Of("code", definition.Code),
                AuditAttribute.Change("status", before, definition.Status)!,
            ],
        };
}
