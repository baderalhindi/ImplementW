using PMPlatform.Domain.Common;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

/// <summary>
/// An instance's runtime state (ADM-040, ADM-047): availability and freshness as two indicators, never one combined health (Blueprint
/// §17.1, BR-INT-013), beside activation, which is neither, and its queued work (TASK-092). <c>AvailabilityChangedAt</c> is since when the
/// availability has held, null before its first change. The endpoint is named by its configuration key; no secret is ever here.
/// </summary>
public sealed record IntegrationInstanceView(
    Guid Id,
    string Code,
    string EndpointConfigurationKey,
    bool IsEnabled,
    IntegrationAvailability Availability,
    IntegrationFreshness Freshness,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    DateTimeOffset? EvaluatedAt,
    int OpenAlertCount,
    DateTimeOffset? AvailabilityChangedAt,
    IntegrationQueueState Queue);

/// <summary>
/// An instance's queued work as FG-05 records it (FG-05 §6.6; ADM-047): attempts still in flight and operations whose latest attempt waits
/// for a retry — FG-05's own and those another module reports — with when the oldest of them began, beside the operations that ended
/// dead-lettered and wait for an operator. It measures FG-05's records, not a broker: work a module has not attempted yet is not in it
/// (docs/operations/operational-dashboards.md F-3).
/// </summary>
/// <param name="InFlight">Attempts begun and not ended.</param>
/// <param name="AwaitingRetry">Operations whose latest attempt is RETRY_SCHEDULED.</param>
/// <param name="OldestQueuedAt">When the first attempt of the oldest queued operation began; null with nothing queued.</param>
/// <param name="DeadLetters">Operations whose latest attempt is DEAD_LETTER: not in the queue, and not retried until an operator does.</param>
public sealed record IntegrationQueueState(int InFlight, int AwaitingRetry, DateTimeOffset? OldestQueuedAt, int DeadLetters)
{
    public static readonly IntegrationQueueState Empty = new(0, 0, null, 0);

    /// <summary>The queue depth: operations in flight or awaiting a retry.</summary>
    public int Depth => InFlight + AwaitingRetry;
}

/// <summary>ADM-040: one integration of the catalogue with its instances.</summary>
public sealed record IntegrationSummary(
    Guid Id,
    string Code,
    BilingualLabel Name,
    IntegrationDirection Direction,
    IntegrationKind IntegrationKind,
    IntegrationDefinitionStatus Status,
    IReadOnlyList<IntegrationInstanceView> Instances);

/// <summary>
/// The integration catalogue, with the environment it describes (FG-05 §4.3: environment context is always visible). Each environment has
/// its own database, so every instance listed is this environment's.
/// </summary>
public sealed record IntegrationCatalogueView(string Environment, IReadOnlyList<IntegrationSummary> Items);

/// <summary>
/// ADR-004's measures of a provider over a window: delivery rate, failure rate, call latency and receipt latency. A measure with no
/// sample is null, never zero (Blueprint safety rule: missing ≠ zero).
/// </summary>
/// <param name="IntegrationInstanceId">The instance measured.</param>
/// <param name="From">The window's start, included.</param>
/// <param name="To">The window's end, excluded.</param>
/// <param name="Attempts">Attempts that ended in the window.</param>
/// <param name="FailedAttempts">Those that failed, whatever the reason.</param>
/// <param name="FailureRate">FailedAttempts / Attempts.</param>
/// <param name="DeliveredLineages">Operations (lineages) whose latest attempt succeeded.</param>
/// <param name="UndeliveredLineages">Operations that ended without success: failed or dead-lettered. Those still retrying are neither.</param>
/// <param name="DeliveryRate">Delivered / (delivered + undelivered).</param>
/// <param name="MedianLatencyMilliseconds">The median duration of an attempt.</param>
/// <param name="P95LatencyMilliseconds">The 95th percentile duration of an attempt.</param>
/// <param name="Receipts">Delivery receipts the provider sent back (SMS, TASK-103).</param>
/// <param name="MedianReceiptLatencyMilliseconds">The median time from a send's first attempt to its receipt.</param>
public sealed record IntegrationMetrics(
    Guid IntegrationInstanceId,
    DateTimeOffset From,
    DateTimeOffset To,
    int Attempts,
    int FailedAttempts,
    double? FailureRate,
    int DeliveredLineages,
    int UndeliveredLineages,
    double? DeliveryRate,
    double? MedianLatencyMilliseconds,
    double? P95LatencyMilliseconds,
    int Receipts,
    double? MedianReceiptLatencyMilliseconds);

/// <summary>ADM-046: the definition with its retry policy, its instances, and each instance's measures over the window asked for.</summary>
public sealed record IntegrationDetail(
    IntegrationSummary Integration,
    Guid? OwnerRoleId,
    short MaxAttempts,
    int RetryBackoffSeconds,
    IReadOnlyList<string> Operations,
    IReadOnlyList<IntegrationMetrics> Metrics);
