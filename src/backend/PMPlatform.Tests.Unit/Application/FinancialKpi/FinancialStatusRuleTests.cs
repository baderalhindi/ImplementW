using PMPlatform.Application.Features.FinancialKpi;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Tests.Unit.Application.FinancialKpi;

/// <summary>The financial status: the forecast's overrun of the Approved Budget; UNKNOWN whenever a figure is not there to rate.</summary>
public sealed class FinancialStatusRuleTests
{
    private static readonly FinancialThresholds Thresholds = new(5m, 10m);

    [Theory]
    [InlineData("1000000.00", "1040000.00", FinancialStatus.Green)]
    [InlineData("1000000.00", "900000.00", FinancialStatus.Green)]
    [InlineData("1000000.00", "1050000.00", FinancialStatus.Amber)]
    [InlineData("1000000.00", "1099999.99", FinancialStatus.Amber)]
    [InlineData("1000000.00", "1100000.00", FinancialStatus.Red)]
    public void TheForecastOverrunIsRatedAgainstTheThresholds(string budget, string forecast, FinancialStatus expected) =>
        Assert.Equal(expected, FinancialStatusRule.Rate(Sar(budget), Sar(forecast), ValueStatus.Measured, Thresholds));

    /// <summary>TASK-052: a missing, stale or not-applicable figure is never read as zero or as GREEN.</summary>
    [Theory]
    [InlineData(ValueStatus.Missing)]
    [InlineData(ValueStatus.Stale)]
    [InlineData(ValueStatus.NotApplicable)]
    public void AFigureThatIsNotMeasuredRatesUnknown(ValueStatus status) =>
        Assert.Equal(FinancialStatus.Unknown, FinancialStatusRule.Rate(Sar("1000000.00"), Sar("900000.00"), status, Thresholds));

    [Fact]
    public void NoBudgetNoForecastOrAZeroBudgetRatesUnknown()
    {
        Assert.Equal(FinancialStatus.Unknown, FinancialStatusRule.Rate(null, Sar("900000.00"), ValueStatus.Measured, Thresholds));
        Assert.Equal(FinancialStatus.Unknown, FinancialStatusRule.Rate(Sar("1000000.00"), null, ValueStatus.Measured, Thresholds));
        Assert.Equal(FinancialStatus.Unknown, FinancialStatusRule.Rate(Money.Zero, Sar("10.00"), ValueStatus.Measured, Thresholds));
    }

    private static Money Sar(string amount) => new(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));
}
