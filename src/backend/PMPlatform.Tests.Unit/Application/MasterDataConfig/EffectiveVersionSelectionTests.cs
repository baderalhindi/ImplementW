using PMPlatform.Application.Features.MasterDataConfig;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;

namespace PMPlatform.Tests.Unit.Application.MasterDataConfig;

/// <summary>
/// TASK-034 acceptance criterion 2: resolving for a past transaction date returns the version effective at that date,
/// not the current one. Resolution is by date alone, and it never guesses (ERD D-13).
/// </summary>
public sealed class EffectiveVersionSelectionTests
{
    private static readonly DateTimeOffset January = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset March = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset June = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly PublishedVersionWindow Version1 = new(Guid.Parse("00000000-0000-4000-8000-000000000001"), 1, January, null);
    private static readonly PublishedVersionWindow Version2 = new(Guid.Parse("00000000-0000-4000-8000-000000000002"), 2, March, null);

    [Fact]
    public void APastDateResolvesToTheVersionEffectiveThenNotTheCurrentOne()
    {
        PublishedVersionWindow[] windows = [Version2, Version1];

        Assert.Equal(Version1, EffectiveVersionSelection.Select(windows, March.AddTicks(-1)).Window);
        Assert.Equal(Version1, EffectiveVersionSelection.Select(windows, January).Window);
        Assert.Equal(Version2, EffectiveVersionSelection.Select(windows, March).Window);
        Assert.Equal(Version2, EffectiveVersionSelection.Select(windows, June).Window);
    }

    [Fact]
    public void BeforeTheFirstVersionNothingIsEffective() =>
        Assert.Equal(EffectiveVersionChoice.None, EffectiveVersionSelection.Select([Version1, Version2], January.AddTicks(-1)));

    [Fact]
    public void AFutureVersionIsNotEffectiveBeforeItsDate()
    {
        PublishedVersionWindow future = Version2 with { EffectiveFrom = June };

        Assert.Equal(Version1, EffectiveVersionSelection.Select([Version1, future], March).Window);
        Assert.Equal(ConfigurationEffectivity.FutureEffective, EffectiveVersionSelection.EffectivityOf(future, [Version1, future], March));
        Assert.Equal(ConfigurationEffectivity.Active, EffectiveVersionSelection.EffectivityOf(Version1, [Version1, future], March));
        Assert.Equal(ConfigurationEffectivity.Superseded, EffectiveVersionSelection.EffectivityOf(Version1, [Version1, future], June));
    }

    /// <summary>Withdrawing the current version leaves nothing effective; the version before it is never brought back.</summary>
    [Fact]
    public void AWithdrawnVersionLeavesNothingEffectiveFromThenOn()
    {
        PublishedVersionWindow withdrawn = Version2 with { EffectiveTo = June };
        PublishedVersionWindow[] windows = [Version1, withdrawn];

        Assert.Equal(withdrawn, EffectiveVersionSelection.Select(windows, June.AddTicks(-1)).Window);
        Assert.Equal(EffectiveVersionChoice.None, EffectiveVersionSelection.Select(windows, June));
        Assert.Equal(Version1, EffectiveVersionSelection.Select(windows, March.AddTicks(-1)).Window);
        Assert.Equal(ConfigurationEffectivity.Retired, EffectiveVersionSelection.EffectivityOf(withdrawn, windows, June));
    }

    /// <summary>A version withdrawn before it took effect never resolves, and the one before it stays in effect.</summary>
    [Fact]
    public void AVersionWithdrawnBeforeTakingEffectNeverResolves()
    {
        PublishedVersionWindow withdrawn = Version2 with { EffectiveTo = March };
        PublishedVersionWindow[] windows = [Version1, withdrawn];

        Assert.Equal(Version1, EffectiveVersionSelection.Select(windows, June).Window);
        Assert.Equal(ConfigurationEffectivity.Active, EffectiveVersionSelection.EffectivityOf(Version1, windows, June));
        Assert.Equal(ConfigurationEffectivity.Retired, EffectiveVersionSelection.EffectivityOf(withdrawn, windows, January));
    }

    /// <summary>Publication prevents two versions sharing a moment; if the data ever holds them, resolution refuses to pick.</summary>
    [Fact]
    public void TwoVersionsClaimingTheSameMomentAreAmbiguous() =>
        Assert.True(EffectiveVersionSelection.Select([Version1, Version2 with { EffectiveFrom = January }], March).IsAmbiguous);

    [Fact]
    public void TheOrderTheVersionsArriveInDoesNotMatter()
    {
        PublishedVersionWindow version3 = new(Guid.NewGuid(), 3, June, null);

        PublishedVersionWindow[][] orders = [[Version1, Version2, version3], [version3, Version1, Version2], [Version2, version3, Version1]];

        Assert.All(
            orders,
            windows => Assert.Equal(Version2, EffectiveVersionSelection.Select(windows, June.AddTicks(-1)).Window));
    }
}
