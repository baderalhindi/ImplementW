using PMPlatform.Application.Features.ChangeRequest;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.ChangeRequest;

/// <summary>
/// ADR-016's rule as <see cref="ChangeMateriality"/> applies it to one profile's three bands: band 2 at 5% of the budget or 10 days,
/// band 3 at 200,000 SAR or 50% of the duration, and a scope rule on band 3 only. No value here is AHDA's (OQ-013).
/// </summary>
public sealed class ChangeMaterialityTests
{
    private static readonly Guid Profile = Guid.NewGuid();

    private static readonly MaterialityBandEntry[] Bands =
    [
        new(Profile, 1, null, null, null, null, null, false),
        new(Profile, 2, 5m, null, null, 10, null, true),
        new(Profile, 3, null, 200000m, 50m, null, "ANY_SCOPE_CHANGE", true),
    ];

    /// <summary>A dimension the request states no impact on has no band; a request with none is band 1.</summary>
    [Fact]
    public void ADimensionWithoutAnImpactHasNoBand() =>
        Assert.Equal(new MaterialityBands(null, null, null, 1), ChangeMateriality.Classify(Bands, Inputs()));

    /// <summary>Below every threshold is band 1; at a band's threshold, that band — the absolute and the percentage are alternatives.</summary>
    [Theory]
    [InlineData("49999.99", (short)1)]
    [InlineData("50000.00", (short)2)]
    [InlineData("199999.99", (short)2)]
    [InlineData("200000.00", (short)3)]
    public void CostTakesTheHighestBandItReaches(string cumulative, short band) =>
        Assert.Equal(band, ChangeMateriality.Classify(Bands, Inputs(cost: decimal.Parse(cumulative, System.Globalization.CultureInfo.InvariantCulture))).Cost);

    [Theory]
    [InlineData(9, (short)1)]
    [InlineData(10, (short)2)]
    [InlineData(29, (short)2)]
    [InlineData(30, (short)3)]
    public void ScheduleTakesTheHighestBandItReaches(int cumulativeDays, short band) =>
        Assert.Equal(band, ChangeMateriality.Classify(Bands, Inputs(days: cumulativeDays, duration: 60)).Schedule);

    /// <summary>A reduction is a change too: the absolute position is judged.</summary>
    [Fact]
    public void AReductionIsJudgedByItsSize()
    {
        MaterialityBands bands = ChangeMateriality.Classify(Bands, Inputs(cost: -60000m, days: -12, duration: 60));
        Assert.Equal(((short?)2, (short?)2), (bands.Cost, bands.Schedule));
    }

    /// <summary>The highest band any dimension triggers wins.</summary>
    [Fact]
    public void TheResultIsTheHighestDimension()
    {
        Assert.Equal(new MaterialityBands(1, 3, null, 3), ChangeMateriality.Classify(Bands, Inputs(cost: 1000m, days: 40, duration: 60)));
        Assert.Equal(new MaterialityBands(2, 1, null, 2), ChangeMateriality.Classify(Bands, Inputs(cost: 60000m, days: 1, duration: 60)));
    }

    /// <summary>Scope escalates by rule: to the highest band naming one; with no rule anywhere, band 1.</summary>
    [Fact]
    public void ScopeTakesTheHighestBandNamingARule()
    {
        Assert.Equal(new MaterialityBands(null, null, 3, 3), ChangeMateriality.Classify(Bands, Inputs(scope: true)));
        MaterialityBandEntry[] noRule = [.. Bands.Select(b => b with { ScopeRuleCode = null })];
        Assert.Equal(new MaterialityBands(null, null, 1, 1), ChangeMateriality.Classify(noRule, Inputs(scope: true)));
    }

    /// <summary>A contractual obligation is band 3 whatever its size (TASK-106).</summary>
    [Fact]
    public void AContractualObligationIsBandThree() =>
        Assert.Equal(new MaterialityBands(1, null, null, 3), ChangeMateriality.Classify(Bands, Inputs(cost: 1m, contractual: true)));

    /// <summary>Missing is never read as zero: a dimension that applies without its base is an error, not band 1.</summary>
    [Fact]
    public void ADimensionWithoutItsBaseIsNotJudged() =>
        Assert.Throws<InvalidOperationException>(() => ChangeMateriality.Classify(Bands, Inputs(days: 5, duration: null)));

    private static MaterialityInputs Inputs(decimal? cost = null, int? days = null, int? duration = 60, bool scope = false, bool contractual = false) =>
        new(new Money(cost ?? 0m), cost is not null, new Money(1000000m), days ?? 0, days is not null, duration, scope, contractual);
}
