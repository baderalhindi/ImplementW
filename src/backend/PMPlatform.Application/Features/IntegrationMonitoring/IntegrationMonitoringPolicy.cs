namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// Configuration section <c>IntegrationMonitoring:Policy</c>. The thresholds are AHDA's (TBC-INT-08 to -12): none is invented, so by
/// default no freshness window and no minimum delivery rate is set, freshness stays UNKNOWN and no delivery-rate alert is raised.
/// </summary>
public sealed class IntegrationMonitoringPolicy
{
    public const string Section = "IntegrationMonitoring:Policy";

    /// <summary>Per definition code, how long after its last success an integration is still FRESH (TBC-INT-12).</summary>
    public Dictionary<string, TimeSpan> FreshnessWindows { get; } = new(StringComparer.Ordinal);

    /// <summary>Per definition code, the delivery rate (0 to 1) below which a DELIVERY_RATE alert is raised (TBC-INT-10; ADR-004).</summary>
    public Dictionary<string, double> MinimumDeliveryRates { get; } = new(StringComparer.Ordinal);

    /// <summary>The window the delivery rate is measured over, and the detail screen's default.</summary>
    public TimeSpan MetricsWindow { get; set; } = TimeSpan.FromHours(24);

    /// <summary>An FG-05 attempt still IN_PROGRESS after this has lost its process; it is ended as a technical failure and retried.</summary>
    public TimeSpan AbandonAfter { get; set; } = TimeSpan.FromHours(1);

    public bool IsValid() =>
        MetricsWindow > TimeSpan.Zero && AbandonAfter > TimeSpan.Zero
        && FreshnessWindows.Values.All(w => w > TimeSpan.Zero)
        && MinimumDeliveryRates.Values.All(r => r is > 0 and <= 1);
}
