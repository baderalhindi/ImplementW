using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// The health rules (FG-05 §5): availability and freshness, each from its own evidence. Only a technical outcome says anything about
/// availability — a refused password or an unverified identity is the provider answering (§6.3) — and freshness comes from the last
/// success and a configured window, never from availability (BR-INT-013, INT-CC-21).
/// </summary>
public static class IntegrationHealth
{
    /// <summary>
    /// Applies an ended attempt to the instance's evidence and returns the availability before it. The latest technical outcome wins,
    /// whatever order the attempts arrive in; a change of availability is dated by the attempt that made it.
    /// </summary>
    public static IntegrationAvailability Apply(IntegrationInstance instance, Invocation attempt)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(attempt);

        IntegrationAvailability before = instance.Availability;
        if (attempt.CompletedAt is not { } at)
        {
            return before;
        }

        if (attempt.Status == InvocationStatus.Succeeded)
        {
            instance.LastSuccessAt = Latest(instance.LastSuccessAt, at);
        }
        else if (attempt.FailureCategory == InvocationFailureCategory.Technical)
        {
            instance.LastFailureAt = Latest(instance.LastFailureAt, at);
        }

        instance.Availability = AvailabilityOf(instance.LastSuccessAt, instance.LastFailureAt);
        if (instance.Availability != before)
        {
            instance.AvailabilityChangedAt = at;
        }

        return before;
    }

    /// <summary>DOWN when the latest technical outcome is a failure, UP when it is a success, UNKNOWN before either.</summary>
    public static IntegrationAvailability AvailabilityOf(DateTimeOffset? lastSuccessAt, DateTimeOffset? lastFailureAt) => (lastSuccessAt, lastFailureAt) switch
    {
        (null, null) => IntegrationAvailability.Unknown,
        (_, null) => IntegrationAvailability.Up,
        (null, _) => IntegrationAvailability.Down,
        ({ } success, { } failure) => failure > success ? IntegrationAvailability.Down : IntegrationAvailability.Up,
    };

    /// <summary>FRESH within <paramref name="window"/> of the last success, STALE after it; UNKNOWN without a window or a success.</summary>
    public static IntegrationFreshness FreshnessOf(DateTimeOffset? lastSuccessAt, TimeSpan? window, DateTimeOffset now) => (lastSuccessAt, window) switch
    {
        ({ } success, { } span) => now - success <= span ? IntegrationFreshness.Fresh : IntegrationFreshness.Stale,
        _ => IntegrationFreshness.Unknown,
    };

    /// <summary>Whether the integration has been DOWN for at least <paramref name="delay"/> (TBC-INT-08): an availability alert delay has passed.</summary>
    public static bool DownFor(IntegrationInstance instance, TimeSpan delay, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return instance.Availability == IntegrationAvailability.Down && instance.AvailabilityChangedAt is { } since && now - since >= delay;
    }

    /// <summary>Whether the queued work exceeds the threshold (TBC-INT-11): more operations queued than its depth, or the oldest older than its age.</summary>
    public static bool BacklogExceeds(IntegrationQueueState queue, QueueBacklogThreshold threshold, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(threshold);
        return (threshold.Depth is { } depth && queue.Depth > depth)
               || (threshold.OldestAge is { } age && queue.OldestQueuedAt is { } oldest && now - oldest > age);
    }

    private static DateTimeOffset Latest(DateTimeOffset? current, DateTimeOffset candidate) =>
        current is { } value && value > candidate ? value : candidate;
}
