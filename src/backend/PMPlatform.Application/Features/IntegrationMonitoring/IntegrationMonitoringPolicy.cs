namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// Configuration section <c>IntegrationMonitoring:Policy</c>. The thresholds are AHDA's (TBC-INT-08 to -12, -15; PTBC-037): none is
/// invented. Without a value, freshness stays UNKNOWN, no delivery-rate or queue-backlog alert is raised, an integration that goes DOWN is
/// alerted at once and any reconciliation mismatch is alerted — the behaviour before any threshold existed
/// (docs/operations/alert-thresholds.json).
/// </summary>
public sealed class IntegrationMonitoringPolicy
{
    public const string Section = "IntegrationMonitoring:Policy";

    /// <summary>Per definition code, how long after its last success an integration is still FRESH (TBC-INT-12).</summary>
    public Dictionary<string, TimeSpan> FreshnessWindows { get; } = new(StringComparer.Ordinal);

    /// <summary>Per definition code, the delivery rate (0 to 1) below which a DELIVERY_RATE alert is raised (TBC-INT-10; ADR-004).</summary>
    public Dictionary<string, double> MinimumDeliveryRates { get; } = new(StringComparer.Ordinal);

    /// <summary>Per definition code, how long an integration stays DOWN before an AVAILABILITY alert is raised (TBC-INT-08). Without one, at once.</summary>
    public Dictionary<string, TimeSpan> AvailabilityAlertDelays { get; } = new(StringComparer.Ordinal);

    /// <summary>Per definition code, the queued work beyond which a QUEUE_BACKLOG alert is raised (TBC-INT-11). Without one, none.</summary>
    public Dictionary<string, QueueBacklogThreshold> QueueBacklogThresholds { get; } = new(StringComparer.Ordinal);

    /// <summary>Per definition code, how many mismatches a reconciliation may find without an alert (TBC-INT-15). Without one, none.</summary>
    public Dictionary<string, int> ReconciliationMismatchTolerances { get; } = new(StringComparer.Ordinal);

    /// <summary>The window the delivery rate is measured over, and the detail screen's default.</summary>
    public TimeSpan MetricsWindow { get; set; } = TimeSpan.FromHours(24);

    /// <summary>An FG-05 attempt still IN_PROGRESS after this has lost its process; it is ended as a technical failure and retried.</summary>
    public TimeSpan AbandonAfter { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan? AvailabilityAlertDelayOf(string code) => AvailabilityAlertDelays.TryGetValue(code, out TimeSpan delay) ? delay : null;

    public int MismatchToleranceOf(string code) => ReconciliationMismatchTolerances.GetValueOrDefault(code);

    public bool IsValid() =>
        MetricsWindow > TimeSpan.Zero && AbandonAfter > TimeSpan.Zero
        && FreshnessWindows.Values.All(w => w > TimeSpan.Zero)
        && MinimumDeliveryRates.Values.All(r => r is > 0 and <= 1)
        && AvailabilityAlertDelays.Values.All(d => d > TimeSpan.Zero)
        && QueueBacklogThresholds.Values.All(t => t.IsValid())
        && ReconciliationMismatchTolerances.Values.All(t => t >= 0);
}

/// <summary>
/// An integration's backlog threshold (FG-05 §10.1 "queue backlog/oldest-message age exceeds configured policy"): either bound, or both.
/// Exceeded when more operations are queued than <see cref="Depth"/>, or the oldest has waited longer than <see cref="OldestAge"/>.
/// </summary>
public sealed class QueueBacklogThreshold
{
    public int? Depth { get; set; }

    public TimeSpan? OldestAge { get; set; }

    public bool IsValid() => (Depth is not null || OldestAge is not null) && Depth is null or >= 0 && (OldestAge is null || OldestAge > TimeSpan.Zero);
}
