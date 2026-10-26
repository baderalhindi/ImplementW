using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts.Events;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>ADM-040 and ADM-046 (TASK-075), and MOD-101's activation (TASK-076). Reads and activation only; nothing here touches an adapter.</summary>
internal sealed partial class IntegrationCatalogueService(
    IIntegrationMonitoringRepository repository,
    IntegrationInvocationEngine engine,
    IntegrationMonitoringPolicy policy,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<IntegrationCatalogueService> logger) : IIntegrationCatalogue
{
    public async Task<IReadOnlyList<IntegrationSummary>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<IntegrationDefinition> definitions = await repository.ListDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IntegrationInstance> instances = await repository.ListInstancesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, int> liveAlerts = await repository.CountLiveAlertsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, IntegrationQueueState> queues = await repository.MeasureQueuesAsync(cancellationToken).ConfigureAwait(false);
        return [.. definitions.Select(d => IntegrationMonitoringMapping.ToSummary(d, instances, liveAlerts, queues))];
    }

    public async Task<AdministrationResult<Versioned<IntegrationDetail>>> GetAsync(
        Guid integrationId, DateTimeOffset? windowStart, DateTimeOffset? windowEnd, CancellationToken cancellationToken) =>
        await repository.FindDefinitionAsync(integrationId, null, cancellationToken).ConfigureAwait(false) is { } definition
            ? await DetailAsync(definition, windowStart, windowEnd, cancellationToken).ConfigureAwait(false)
            : AdministrationError.NotFound;

    public Task<AdministrationResult<Versioned<IntegrationDetail>>> EnableAsync(Guid actorId, Guid integrationId, uint? expectedVersion, CancellationToken cancellationToken) =>
        ChangeActivationAsync(actorId, integrationId, expectedVersion, IntegrationDefinitionStatus.Active, IntegrationMonitoringAuditEvents.IntegrationEnabled, cancellationToken);

    public Task<AdministrationResult<Versioned<IntegrationDetail>>> DisableAsync(Guid actorId, Guid integrationId, uint? expectedVersion, CancellationToken cancellationToken) =>
        ChangeActivationAsync(actorId, integrationId, expectedVersion, IntegrationDefinitionStatus.Disabled, IntegrationMonitoringAuditEvents.IntegrationDisabled, cancellationToken);

    private async Task<AdministrationResult<Versioned<IntegrationDetail>>> ChangeActivationAsync(
        Guid actorId, Guid integrationId, uint? expectedVersion, IntegrationDefinitionStatus target, string eventType, CancellationToken cancellationToken)
    {
        if (await repository.FindDefinitionAsync(integrationId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (definition.Status == IntegrationDefinitionStatus.Retired)
        {
            return AdministrationError.TerminalState;
        }

        if (definition.Status == target)
        {
            return AdministrationError.InvalidTransition;
        }

        IntegrationDefinitionStatus before = definition.Status;
        DateTimeOffset now = timeProvider.GetUtcNow();
        definition.Status = target;
        definition.UpdatedAt = now;
        definition.UpdatedBy = actorId;
        audit.Stage(IntegrationMonitoringAudit.ActivationChanged(eventType, actorId, definition, before));
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != IntegrationMonitoringSaveOutcome.Saved)
        {
            return AdministrationError.PreconditionFailed;
        }

        LogActivationChanged(logger, actorId, definition.Code, target);
        return await DetailAsync(definition, null, null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Versioned<IntegrationDetail>> DetailAsync(IntegrationDefinition definition, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        DateTimeOffset end = to ?? timeProvider.GetUtcNow();
        DateTimeOffset start = from ?? (end - policy.MetricsWindow);
        IReadOnlyList<IntegrationInstance> instances = [.. (await repository.ListInstancesAsync(cancellationToken).ConfigureAwait(false))
            .Where(i => i.IntegrationDefinitionId == definition.Id)];
        IReadOnlyDictionary<Guid, int> liveAlerts = await repository.CountLiveAlertsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, IntegrationQueueState> queues = await repository.MeasureQueuesAsync(cancellationToken).ConfigureAwait(false);
        List<IntegrationMetrics> metrics = [];
        foreach (IntegrationInstance instance in instances.OrderBy(i => i.Code, StringComparer.Ordinal))
        {
            InvocationStatistics statistics = await repository.MeasureAsync(instance.Id, start, end, cancellationToken).ConfigureAwait(false);
            metrics.Add(IntegrationMetricsCalculator.From(instance.Id, start, end, statistics));
        }

        IntegrationDetail detail = new(
            IntegrationMonitoringMapping.ToSummary(definition, instances, liveAlerts, queues), definition.OwnerRoleId, definition.MaxAttempts, definition.RetryBackoffSeconds,
            engine.OperationsOf(definition.Code), metrics);
        return new Versioned<IntegrationDetail>(detail, repository.RowVersionOf(definition));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Integration monitoring: {ActorId} made {Integration} {Status}.")]
    private static partial void LogActivationChanged(ILogger logger, Guid actorId, string integration, IntegrationDefinitionStatus status);
}
