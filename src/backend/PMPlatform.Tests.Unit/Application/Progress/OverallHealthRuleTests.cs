using PMPlatform.Application.Features.Progress;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Tests.Unit.Application.Progress;

/// <summary>ICD-03's calculation: progress slippage against the configured thresholds, combined with Schedule Health and financial status.</summary>
public sealed class OverallHealthRuleTests
{
    private static readonly HealthThresholds Thresholds = new(5, 15);

    [Theory]
    [InlineData(60, 50, HealthStatus.Green)]   // ahead of plan
    [InlineData(50, 50, HealthStatus.Green)]
    [InlineData(45.0001, 50, HealthStatus.Green)]
    [InlineData(45, 50, HealthStatus.Amber)]   // AMBER from 5 points behind
    [InlineData(35.0001, 50, HealthStatus.Amber)]
    [InlineData(35, 50, HealthStatus.Red)]     // RED from 15 points behind
    [InlineData(0, 100, HealthStatus.Red)]
    public void ProgressIsRatedOnItsSlippageBehindPlan(decimal actual, decimal planned, HealthStatus expected) =>
        Assert.Equal(expected, OverallHealthRule.Progress(actual, planned, Thresholds));

    [Theory]
    [InlineData(null, 50.0)]
    [InlineData(50.0, null)]
    public void ProgressWithoutBothFiguresIsUnknown(double? actual, double? planned) =>
        Assert.Equal(HealthStatus.Unknown, OverallHealthRule.Progress((decimal?)actual, (decimal?)planned, Thresholds));

    [Theory]
    [InlineData(HealthStatus.Green, HealthStatus.Green, HealthStatus.Green)]
    [InlineData(HealthStatus.Amber, HealthStatus.Green, HealthStatus.Amber)]
    [InlineData(HealthStatus.Green, HealthStatus.Red, HealthStatus.Red)]
    [InlineData(HealthStatus.Red, HealthStatus.Amber, HealthStatus.Red)]
    public void OverallIsTheWorstOfTheThreeDimensions(HealthStatus schedule, HealthStatus financial, HealthStatus expected) =>
        Assert.Equal(expected, OverallHealthRule.Compute(50, 50, schedule, financial, Thresholds));

    [Fact]
    public void OverallTakesProgressSlippageIntoAccount() =>
        Assert.Equal(HealthStatus.Red, OverallHealthRule.Compute(10, 50, HealthStatus.Green, HealthStatus.Green, Thresholds));

    /// <summary>"UNKNOWN when inputs are missing — never coerced" (ERD): a missing dimension is not read as GREEN, even beside a RED one.</summary>
    [Theory]
    [InlineData(null, HealthStatus.Green, HealthStatus.Green)]
    [InlineData(50.0, null, HealthStatus.Green)]
    [InlineData(50.0, HealthStatus.Green, null)]
    [InlineData(50.0, HealthStatus.Unknown, HealthStatus.Red)]
    public void AMissingInputMakesOverallUnknown(double? planned, HealthStatus? schedule, HealthStatus? financial) =>
        Assert.Equal(HealthStatus.Unknown, OverallHealthRule.Compute(50, (decimal?)planned, schedule, financial, Thresholds));
}
