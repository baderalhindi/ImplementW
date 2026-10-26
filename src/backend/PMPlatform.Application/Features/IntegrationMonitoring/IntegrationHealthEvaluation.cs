using System.Globalization;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// The time-driven half of health (FG-05 §5.4, §10.1): freshness lapses without any new attempt, a delivery rate is measured over a
/// window, an integration stays down past its availability alert delay, and work waits in its queue, so a pass re-evaluates every
/// instance and raises FRESHNESS, DELIVERY_RATE, AVAILABILITY and QUEUE_BACKLOG alerts against the policy's thresholds — none by default
/// (TBC-INT-08, -10, -11, -12; PTBC-037). Each instance is evaluated in its own transaction with its row locked; one that fails does not
/// stop the others. The pass writes only <c>integration_monitoring</c>.
/// </summary>
internal sealed partial class IntegrationHealthEvaluation(
    IIntegrationMonitoringRepository repository,
    IntegrationHealthRecorder health,
    OperationalAlerting alerting,
    IntegrationMonitoringPolicy policy,
    TimeProvider timeProvider,
    ILogger<IntegrationHealthEvaluation> logger)
{
    /// <summary>Evaluates every instance; returns how many it evaluated.</summary>
    public async Task<int> EvaluateAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<IntegrationDefinition> definitions = await repository.ListDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IntegrationInstance> instances = await repository.ListInstancesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, IntegrationQueueState> queues = await repository.MeasureQueuesAsync(cancellationToken).ConfigureAwait(false);
        int evaluated = 0;
        foreach (IntegrationInstance listed in instances)
        {
            IntegrationDefinition definition = definitions.Single(d => d.Id == listed.IntegrationDefinitionId);
            try
            {
                await EvaluateAsync(definition, listed.Id, queues.GetValueOrDefault(listed.Id) ?? IntegrationQueueState.Empty, cancellationToken).ConfigureAwait(false);
                evaluated++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // One instance that cannot be evaluated must not stop the pass for the others.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogEvaluationFailed(logger, definition.Code, listed.Code, exception.GetType().Name);
            }
        }

        return evaluated;
    }

    private async Task EvaluateAsync(IntegrationDefinition definition, Guid instanceId, IntegrationQueueState queue, CancellationToken cancellationToken)
    {
        await using IIntegrationMonitoringWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        IntegrationInstance instance = (await repository.LockInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false))!;
        DateTimeOffset now = timeProvider.GetUtcNow();

        IntegrationFreshness before = instance.Freshness;
        instance.Freshness = IntegrationHealth.FreshnessOf(instance.LastSuccessAt, health.WindowOf(definition), now);
        if (before != IntegrationFreshness.Stale && instance.Freshness == IntegrationFreshness.Stale)
        {
            await alerting.RaiseAsync(
                definition, instance, OperationalAlertType.Freshness,
                string.Create(CultureInfo.InvariantCulture, $"{definition.Code} has not succeeded since {instance.LastSuccessAt:O}, beyond its freshness window of {health.WindowOf(definition)}"),
                null, null, cancellationToken).ConfigureAwait(false);
        }

        if (policy.MinimumDeliveryRates.TryGetValue(definition.Code, out double minimum))
        {
            DateTimeOffset from = now - policy.MetricsWindow;
            InvocationStatistics statistics = await repository.MeasureAsync(instance.Id, from, now, cancellationToken).ConfigureAwait(false);
            if (IntegrationMetricsCalculator.From(instance.Id, from, now, statistics).DeliveryRate is { } rate && rate < minimum)
            {
                await alerting.RaiseAsync(
                    definition, instance, OperationalAlertType.DeliveryRate,
                    string.Create(CultureInfo.InvariantCulture,
                        $"{definition.Code} delivered {statistics.DeliveredLineages} of {statistics.DeliveredLineages + statistics.UndeliveredLineages} ({rate:P1}) since {from:O}, below {minimum:P1}"),
                    null, null, cancellationToken).ConfigureAwait(false);
            }
        }

        // One AVAILABILITY alert per outage: none if one was raised since the integration went down, even if an operator has closed it.
        if (policy.AvailabilityAlertDelayOf(definition.Code) is { } delay && IntegrationHealth.DownFor(instance, delay, now)
            && !await repository.AlertRaisedSinceAsync(instance.Id, OperationalAlertType.Availability, instance.AvailabilityChangedAt!.Value, cancellationToken).ConfigureAwait(false))
        {
            await alerting.RaiseAsync(
                definition, instance, OperationalAlertType.Availability,
                string.Create(CultureInfo.InvariantCulture, $"{definition.Code} has been down since {instance.AvailabilityChangedAt:O}, beyond its availability alert delay of {delay}"),
                null, null, cancellationToken).ConfigureAwait(false);
        }

        if (policy.QueueBacklogThresholds.TryGetValue(definition.Code, out QueueBacklogThreshold? threshold) && IntegrationHealth.BacklogExceeds(queue, threshold, now))
        {
            await alerting.RaiseAsync(
                definition, instance, OperationalAlertType.QueueBacklog,
                string.Create(CultureInfo.InvariantCulture,
                    $"{definition.Code} has {queue.Depth} operations queued ({queue.InFlight} in flight, {queue.AwaitingRetry} awaiting a retry), the oldest since {queue.OldestQueuedAt:O}, beyond its backlog threshold (depth {threshold.Depth?.ToString(CultureInfo.InvariantCulture) ?? "none"}, oldest age {threshold.OldestAge?.ToString() ?? "none"})"),
                null, null, cancellationToken).ConfigureAwait(false);
        }

        instance.EvaluatedAt = now;
        instance.UpdatedAt = now;
        instance.UpdatedBy = IntegrationMonitoringServicePrincipal.Id;
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == IntegrationMonitoringSaveOutcome.Saved)
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Integration health of {Integration} {Instance} was not evaluated: {Reason}. It is tried again on the next pass.")]
    private static partial void LogEvaluationFailed(ILogger logger, string integration, string instance, string reason);
}
