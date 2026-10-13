using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

internal static class IntegrationMonitoringMapping
{
    public static IntegrationInstanceView ToView(IntegrationInstance instance, IReadOnlyDictionary<Guid, int> liveAlerts) =>
        new(instance.Id, instance.Code, instance.EndpointConfigurationKey, instance.IsEnabled, instance.Availability, instance.Freshness,
            instance.LastSuccessAt, instance.LastFailureAt, instance.EvaluatedAt, liveAlerts.TryGetValue(instance.Id, out int count) ? count : 0);

    public static IntegrationSummary ToSummary(IntegrationDefinition definition, IEnumerable<IntegrationInstance> instances, IReadOnlyDictionary<Guid, int> liveAlerts) =>
        new(definition.Id, definition.Code, definition.Name, definition.Direction, definition.IntegrationKind, definition.Status,
            [.. instances.Where(i => i.IntegrationDefinitionId == definition.Id).OrderBy(i => i.Code, StringComparer.Ordinal).Select(i => ToView(i, liveAlerts))]);

    public static InvocationSummary ToSummary(Invocation invocation, IReadOnlyDictionary<Guid, string> integrationCodes) =>
        new(invocation.Id, invocation.IntegrationInstanceId, CodeOf(invocation.IntegrationInstanceId, integrationCodes), invocation.SyncRunId, invocation.OperationCode,
            invocation.CorrelationId, invocation.IdempotencyKey, invocation.AttemptNo, invocation.SourceModule, invocation.SourceReference, invocation.StartedAt,
            invocation.CompletedAt, invocation.Status, invocation.FailureCategory, invocation.ResponseCode, invocation.ErrorSummary, invocation.NextAttemptAt);

    public static SyncRunView ToView(SyncRun run, IReadOnlyDictionary<Guid, string> integrationCodes) =>
        new(run.Id, run.IntegrationInstanceId, CodeOf(run.IntegrationInstanceId, integrationCodes), run.RunType, run.CorrelationId, run.StartedAt, run.CompletedAt,
            run.Status, run.RecordsRead, run.RecordsApplied, run.RecordsRejected, run.MismatchCount);

    public static OperationalAlertView ToView(OperationalAlert alert, IReadOnlyDictionary<Guid, string> integrationCodes) =>
        new(alert.Id, alert.IntegrationInstanceId, CodeOf(alert.IntegrationInstanceId, integrationCodes), alert.SyncRunId, alert.InvocationId, alert.AlertType,
            alert.Severity, alert.RaisedAt, alert.Status, alert.AcknowledgedByUserId, alert.AcknowledgedAt, alert.ResolvedAt, alert.ResolutionNote, alert.Detail);

    private static string CodeOf(Guid instanceId, IReadOnlyDictionary<Guid, string> integrationCodes) =>
        integrationCodes.TryGetValue(instanceId, out string? code) ? code : string.Empty;
}
