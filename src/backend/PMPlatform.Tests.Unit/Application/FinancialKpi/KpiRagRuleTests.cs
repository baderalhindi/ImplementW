using PMPlatform.Application.Features.FinancialKpi;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Tests.Unit.Application.FinancialKpi;

/// <summary>A measurement's RAG against its pinned target, by direction; a missing value or missing thresholds are never a colour.</summary>
public sealed class KpiRagRuleTests
{
    [Theory]
    [InlineData(KpiDirection.HigherIsBetter, 96, KpiRagStatus.Green)]
    [InlineData(KpiDirection.HigherIsBetter, 95, KpiRagStatus.Green)]
    [InlineData(KpiDirection.HigherIsBetter, 90, KpiRagStatus.Amber)]
    [InlineData(KpiDirection.HigherIsBetter, 89.9999, KpiRagStatus.Red)]
    [InlineData(KpiDirection.LowerIsBetter, 2, KpiRagStatus.Green)]
    [InlineData(KpiDirection.LowerIsBetter, 5, KpiRagStatus.Amber)]
    [InlineData(KpiDirection.LowerIsBetter, 5.0001, KpiRagStatus.Red)]
    public void AValueIsRatedByItsDirection(KpiDirection direction, double value, KpiRagStatus expected)
    {
        (decimal green, decimal amber) = direction == KpiDirection.HigherIsBetter ? (95m, 90m) : (2m, 5m);
        Assert.Equal(expected, KpiRagRule.Rate((decimal)value, ValueStatus.Measured, direction, 100m, green, amber));
    }

    /// <summary>TARGET_BAND: the thresholds are distances from the target, either side.</summary>
    [Theory]
    [InlineData(100, KpiRagStatus.Green)]
    [InlineData(98, KpiRagStatus.Green)]
    [InlineData(105, KpiRagStatus.Amber)]
    [InlineData(94, KpiRagStatus.Red)]
    public void ABandIsRatedByTheDistanceFromTheTarget(int value, KpiRagStatus expected) =>
        Assert.Equal(expected, KpiRagRule.Rate(value, ValueStatus.Measured, KpiDirection.TargetBand, 100m, 2m, 5m));

    /// <summary>TASK-052: MISSING and STALE are UNKNOWN, NOT_APPLICABLE stays NOT_APPLICABLE — never GREEN, whatever the thresholds.</summary>
    [Theory]
    [InlineData(ValueStatus.Missing, KpiRagStatus.Unknown)]
    [InlineData(ValueStatus.Stale, KpiRagStatus.Unknown)]
    [InlineData(ValueStatus.NotApplicable, KpiRagStatus.NotApplicable)]
    public void NoValueIsNeverAColour(ValueStatus status, KpiRagStatus expected)
    {
        Assert.Equal(expected, KpiRagRule.Rate(null, status, KpiDirection.LowerIsBetter, 0m, 0m, 1m));
        Assert.Equal(expected, KpiRagRule.Rate(null, status, KpiDirection.HigherIsBetter, 0m, 0m, 0m));
    }

    [Fact]
    public void ATargetWithoutThresholdsRatesUnknown() =>
        Assert.Equal(KpiRagStatus.Unknown, KpiRagRule.Rate(100m, ValueStatus.Measured, KpiDirection.HigherIsBetter, 100m, null, null));

    [Theory]
    [InlineData(KpiDirection.HigherIsBetter, 95, 90, true)]
    [InlineData(KpiDirection.HigherIsBetter, 90, 95, false)]
    [InlineData(KpiDirection.LowerIsBetter, 2, 5, true)]
    [InlineData(KpiDirection.LowerIsBetter, 5, 2, false)]
    [InlineData(KpiDirection.TargetBand, 2, 5, true)]
    [InlineData(KpiDirection.TargetBand, -1, 5, false)]
    public void ThresholdsMustOrderAsTheDirectionRequires(KpiDirection direction, int green, int amber, bool coherent) =>
        Assert.Equal(coherent, KpiRagRule.AreCoherent(direction, green, amber));

    [Fact]
    public void ThresholdsComeBothOrNeither()
    {
        Assert.True(KpiRagRule.AreCoherent(KpiDirection.HigherIsBetter, null, null));
        Assert.False(KpiRagRule.AreCoherent(KpiDirection.HigherIsBetter, 95m, null));
        Assert.False(KpiRagRule.AreCoherent(KpiDirection.HigherIsBetter, null, 90m));
    }
}
