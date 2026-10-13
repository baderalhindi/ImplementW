using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// ADR-004's measures from an instance's statistics: a rate over no sample is null, never zero or one (missing ≠ zero). The delivery rate
/// counts operations that ended — succeeded, failed or dead-lettered — and leaves out those still being retried.
/// </summary>
public static class IntegrationMetricsCalculator
{
    /// <summary>The receipt a provider sends back for a delivered message (SMS, TASK-103); its lineage is the send's.</summary>
    public const string DeliveryReceiptOperation = "DELIVERY_RECEIPT";

    public static IntegrationMetrics From(Guid instanceId, DateTimeOffset from, DateTimeOffset to, InvocationStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        int ended = statistics.DeliveredLineages + statistics.UndeliveredLineages;
        return new IntegrationMetrics(
            instanceId,
            from,
            to,
            statistics.Attempts,
            statistics.FailedAttempts,
            Rate(statistics.FailedAttempts, statistics.Attempts),
            statistics.DeliveredLineages,
            statistics.UndeliveredLineages,
            Rate(statistics.DeliveredLineages, ended),
            statistics.MedianLatencyMilliseconds,
            statistics.P95LatencyMilliseconds,
            statistics.Receipts,
            statistics.MedianReceiptLatencyMilliseconds);
    }

    private static double? Rate(int part, int whole) => whole == 0 ? null : (double)part / whole;
}
