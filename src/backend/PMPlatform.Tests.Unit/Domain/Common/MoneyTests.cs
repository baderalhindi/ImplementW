using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Domain.Common;

public sealed class MoneyTests
{
    [Fact]
    public void CurrencyIsSarAndNothingElse()
    {
        Assert.Equal("SAR", Money.CurrencyCode);
        Assert.Equal("1250.50 SAR", new Money(1250.50m).ToString());
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(1.005)]
    public void RejectsMoreThanTwoDecimalPlaces(double amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money((decimal)amount));
    }

    [Fact]
    public void RejectsAmountsBeyondNumeric18Comma2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(10_000_000_000_000_000m));
    }

    [Fact]
    public void AddsAndSubtracts()
    {
        Money sum = new Money(100m) + new Money(0.25m) - new Money(50m);
        Assert.Equal(new Money(50.25m), sum);
    }
}
