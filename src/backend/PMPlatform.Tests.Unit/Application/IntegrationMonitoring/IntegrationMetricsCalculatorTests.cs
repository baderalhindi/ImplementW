using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

namespace PMPlatform.Tests.Unit.Application.IntegrationMonitoring;

/// <summary>ADR-004's measures: rates from counts, null over no sample — never zero, never one (missing ≠ zero).</summary>
public sealed class IntegrationMetricsCalculatorTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RatesComeFromTheCountsAndOperationsStillRetryingAreLeftOut()
    {
        IntegrationMetrics metrics = IntegrationMetricsCalculator.From(Guid.NewGuid(), T, T.AddDays(1), new InvocationStatistics(8, 2, 3, 1, 120, 300, 2, 45_000));

        Assert.Equal((0.25, 0.75), (metrics.FailureRate, metrics.DeliveryRate));
        Assert.Equal((120d, 300d, 45_000d), (metrics.MedianLatencyMilliseconds, metrics.P95LatencyMilliseconds, metrics.MedianReceiptLatencyMilliseconds));
    }

    [Fact]
    public void NoSampleIsNoRate()
    {
        IntegrationMetrics metrics = IntegrationMetricsCalculator.From(Guid.NewGuid(), T, T.AddDays(1), new InvocationStatistics(0, 0, 0, 0, null, null, 0, null));

        Assert.Equal((null, null), (metrics.FailureRate, metrics.DeliveryRate));
    }
}
