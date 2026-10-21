using System.Globalization;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// Applies an ended attempt to its instance's health and raises what it calls for: an AVAILABILITY alert when the integration goes down
/// (unless an availability alert delay is configured: then the health pass raises it once the delay has passed), a DEAD_LETTER alert when a lineage is dead-lettered. Health is an operational projection on the instance row (ERD derived register
/// row 21); a recovery does not resolve an alert, clear a dead letter or claim the data is current (BR-INT-033, DC-INT-10). Called with
/// the instance locked, in the unit of work that records the attempt.
/// </summary>
internal sealed class IntegrationHealthRecorder(OperationalAlerting alerting, IntegrationMonitoringPolicy policy, TimeProvider timeProvider)
{
    public async Task RecordAsync(IntegrationDefinition definition, IntegrationInstance instance, Invocation attempt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(attempt);

        DateTimeOffset now = timeProvider.GetUtcNow();
        IntegrationAvailability before = IntegrationHealth.Apply(instance, attempt);
        instance.Freshness = IntegrationHealth.FreshnessOf(instance.LastSuccessAt, WindowOf(definition), now);
        instance.EvaluatedAt = now;
        instance.UpdatedAt = now;
        instance.UpdatedBy = IntegrationMonitoringServicePrincipal.Id;

        // With an availability alert delay the health pass raises it, once the integration has stayed down that long (TASK-092).
        if (before != IntegrationAvailability.Down && instance.Availability == IntegrationAvailability.Down
            && policy.AvailabilityAlertDelayOf(definition.Code) is null)
        {
            await alerting.RaiseAsync(
                definition, instance, OperationalAlertType.Availability,
                Detail($"{definition.Code} {attempt.OperationCode} failed technically", attempt),
                attempt.Id, attempt.SyncRunId, cancellationToken).ConfigureAwait(false);
        }

        if (attempt.Status == InvocationStatus.DeadLetter)
        {
            await alerting.RaiseAsync(
                definition, instance, OperationalAlertType.DeadLetter,
                Detail($"{definition.Code} {attempt.OperationCode} dead-lettered after attempt {attempt.AttemptNo.ToString(CultureInfo.InvariantCulture)}", attempt),
                attempt.Id, attempt.SyncRunId, cancellationToken).ConfigureAwait(false);
        }
    }

    public TimeSpan? WindowOf(IntegrationDefinition definition) =>
        policy.FreshnessWindows.TryGetValue(definition.Code, out TimeSpan window) ? window : null;

    private static string Detail(string what, Invocation attempt) =>
        string.Join("; ", new[] { what, attempt.ResponseCode is { } code ? $"response {code}" : null, attempt.ErrorSummary is { } fault ? $"fault {fault}" : null }
            .OfType<string>());
}
