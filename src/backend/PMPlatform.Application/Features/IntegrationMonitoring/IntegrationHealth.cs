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
    /// whatever order the attempts arrive in.
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

    private static DateTimeOffset Latest(DateTimeOffset? current, DateTimeOffset candidate) =>
        current is { } value && value > candidate ? value : candidate;
}
