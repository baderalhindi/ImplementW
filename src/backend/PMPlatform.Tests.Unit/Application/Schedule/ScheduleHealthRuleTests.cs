using PMPlatform.Application.Features.Schedule;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Tests.Unit.Application.Schedule;

/// <summary>DCL-SCH-17: Schedule Health from the finish variance and the configured thresholds; UNKNOWN when either is missing.</summary>
public sealed class ScheduleHealthRuleTests
{
    private static readonly ScheduleHealthThresholds Thresholds = new(AmberFinishVarianceDays: 5, RedFinishVarianceDays: 20);

    [Theory]
    [InlineData(-30, ScheduleHealth.Green)]   // early
    [InlineData(0, ScheduleHealth.Green)]
    [InlineData(4, ScheduleHealth.Green)]
    [InlineData(5, ScheduleHealth.Amber)]     // from the AMBER threshold
    [InlineData(19, ScheduleHealth.Amber)]
    [InlineData(20, ScheduleHealth.Red)]      // from the RED threshold
    [InlineData(400, ScheduleHealth.Red)]
    public void TheVarianceIsRatedAgainstTheThresholds(int finishVarianceDays, ScheduleHealth expected) =>
        Assert.Equal(expected, ScheduleHealthRule.Rate(finishVarianceDays, Thresholds));

    [Fact]
    public void AMissingVarianceIsUnknownNotGreen() => Assert.Equal(ScheduleHealth.Unknown, ScheduleHealthRule.Rate(null, Thresholds));

    [Fact]
    public void MissingThresholdsAreUnknownNotGreen() => Assert.Equal(ScheduleHealth.Unknown, ScheduleHealthRule.Rate(0, null));
}
