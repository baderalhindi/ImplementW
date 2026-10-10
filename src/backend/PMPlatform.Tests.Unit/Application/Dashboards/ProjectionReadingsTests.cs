using PMPlatform.Application.Common.Projections;
using PMPlatform.Application.Features.Dashboards;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.Dashboards;

/// <summary>
/// FG-01 §7's rules as one implementation: missing is not zero, stale is not current, unknown is not a colour, and an aggregate counts only what
/// it may and says what it left out (BR-DSH-013 to -016, -024; TASK-069 acceptance criteria 1 and 2).
/// </summary>
public sealed class ProjectionReadingsTests
{
    private static readonly DateTimeOffset Earlier = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyList<string> Order = ["GREEN", "AMBER", "RED", "UNKNOWN"];

    [Fact]
    public void AMissingValueIsUnknownWithNoDataNeverZero()
    {
        ProjectionReading reading = ProjectionReadings.Of(new Observation(Guid.NewGuid(), ObservationKind.Missing, null, null));

        Assert.Equal((ProjectionFreshness.Unknown, WidgetUnknownReason.Missing, ProjectionCoverage.None), (reading.Freshness, reading.UnknownReason, reading.Coverage));
        Assert.Null(reading.Data);
        Assert.Null(reading.AsOf);
    }

    [Fact]
    public void AStaleValueKeepsItsDataAndItsOwnAsOf()
    {
        WidgetData value = ProjectionReadings.State("GREEN");
        ProjectionReading reading = ProjectionReadings.Of(new Observation(Guid.NewGuid(), ObservationKind.Stale, Earlier, value));

        Assert.Equal((ProjectionFreshness.Stale, Earlier), (reading.Freshness, reading.AsOf));
        Assert.Null(reading.UnknownReason);
        Assert.Same(value, reading.Data);
    }

    [Theory]
    [InlineData(nameof(ObservationKind.NotApplicable), WidgetUnknownReason.NotApplicable)]
    [InlineData(nameof(ObservationKind.Masked), WidgetUnknownReason.Restricted)]
    [InlineData(nameof(ObservationKind.Incompatible), WidgetUnknownReason.Missing)]
    public void EveryOtherAbsenceIsUnknownWithItsReason(string kind, WidgetUnknownReason reason)
    {
        ProjectionReading reading = ProjectionReadings.Of(new Observation(Guid.NewGuid(), Enum.Parse<ObservationKind>(kind), Later, ProjectionReadings.State("GREEN")));

        Assert.Equal((ProjectionFreshness.Unknown, reason), (reading.Freshness, reading.UnknownReason));
        Assert.Null(reading.Data);
    }

    /// <summary>An aggregate counts the counted only, lists the rest by reason, and keeps UNKNOWN as the source's own value, never a colour.</summary>
    [Fact]
    public void AnAggregateCountsOnlyWhatItMayAndSaysWhatItLeftOut()
    {
        Observation[] observations =
        [
            Counted("GREEN", Later), Counted("UNKNOWN", Later), Counted("RED", Earlier),
            new(Guid.NewGuid(), ObservationKind.Missing, null, null),
            new(Guid.NewGuid(), ObservationKind.Masked, null, null),
            new(Guid.NewGuid(), ObservationKind.NotApplicable, null, null),
        ];

        ProjectionReading reading = ProjectionReadings.Aggregate(observations, counted => ProjectionReadings.Distribution(counted, Order));

        Assert.Equal((ProjectionFreshness.Fresh, ProjectionCoverage.Partial, Earlier), (reading.Freshness, reading.Coverage, reading.AsOf));
        Assert.Equal(["GREEN|1", "RED|1", "UNKNOWN|1"], reading.Data!.Distribution.Select(b => $"{b.Key}|{b.Count}"));
        WidgetCoverage coverage = reading.CoverageDetail!;
        Assert.Equal((5, 3, 2, 0), (coverage.EligibleCount, coverage.IncludedCount, coverage.ExcludedCount, coverage.StaleCount));
        Assert.Equal([new CoverageExclusion(CoverageExclusionReason.Missing, 1), new CoverageExclusion(CoverageExclusionReason.Masked, 1)], coverage.Exclusions);
    }

    [Fact]
    public void AnyStaleCountedValueMakesTheAggregateStale()
    {
        ProjectionReading reading = ProjectionReadings.Aggregate(
            [Counted("GREEN", Later), new(Guid.NewGuid(), ObservationKind.Stale, Earlier, ProjectionReadings.State("GREEN"))],
            counted => ProjectionReadings.Distribution(counted, Order));

        Assert.Equal((ProjectionFreshness.Stale, ProjectionCoverage.Complete, 1), (reading.Freshness, reading.Coverage, reading.CoverageDetail!.StaleCount));
        Assert.Equal(2, reading.Data!.Distribution.Single().Count);
    }

    [Fact]
    public void AnAggregateWithNothingCountedIsUnknownNotAnEmptyDistribution()
    {
        ProjectionReading missing = ProjectionReadings.Aggregate([new(Guid.NewGuid(), ObservationKind.Missing, null, null)], _ => throw new InvalidOperationException("Nothing to combine."));
        ProjectionReading masked = ProjectionReadings.Aggregate([new(Guid.NewGuid(), ObservationKind.Masked, null, null)], _ => throw new InvalidOperationException("Nothing to combine."));
        ProjectionReading none = ProjectionReadings.Aggregate([new(Guid.NewGuid(), ObservationKind.NotApplicable, null, null)], _ => throw new InvalidOperationException("Nothing to combine."));

        Assert.Equal(WidgetUnknownReason.Missing, missing.UnknownReason);
        Assert.Null(missing.Data);
        Assert.Equal(WidgetUnknownReason.Restricted, masked.UnknownReason);
        Assert.Equal((WidgetUnknownReason.NotApplicable, 0), (none.UnknownReason, none.CoverageDetail!.EligibleCount));
    }

    /// <summary>Counts add across projects; a percentage or an amount is never summed or averaged here (BR-DSH-023).</summary>
    [Fact]
    public void OnlyCountsAreAddedAcrossProjects()
    {
        WidgetData a = new(null, [ProjectionReadings.Count("OPEN_RISKS", 2), ProjectionReadings.Percent("ACTUAL_PERCENT", 40m)], [new WidgetBucket("HIGH", null, 2)], ProjectionReadings.NoSeries);
        WidgetData b = new(null, [ProjectionReadings.Count("OPEN_RISKS", 3), ProjectionReadings.Percent("ACTUAL_PERCENT", 60m)], [new WidgetBucket("HIGH", null, 1), new WidgetBucket("LOW", null, 2)], ProjectionReadings.NoSeries);

        WidgetData sum = ProjectionReadings.SumBuckets([new(Guid.NewGuid(), ObservationKind.Current, Later, a), new(Guid.NewGuid(), ObservationKind.Current, Later, b)], ["LOW", "HIGH"]);

        Assert.Equal(["LOW|2", "HIGH|3"], sum.Distribution.Select(x => $"{x.Key}|{x.Count}"));
        Assert.Equal(["OPEN_RISKS|5"], sum.Figures.Select(f => $"{f.Measure}|{f.Value}"));
    }

    /// <summary>R-16: an amount is its exact two-place string; a masked one carries no value at all.</summary>
    [Fact]
    public void MoneyIsAnExactStringAndAMaskedAmountHasNoValue()
    {
        Assert.Equal(new WidgetFigure("BUDGET", "1234567890123.40", "SAR", false), ProjectionReadings.Sar("BUDGET", new Money(1234567890123.4m), false));
        Assert.Equal(new WidgetFigure("BUDGET", null, "SAR", true), ProjectionReadings.Sar("BUDGET", new Money(1m), true));
        Assert.Equal(new WidgetFigure("BUDGET", null, "SAR", false), ProjectionReadings.Sar("BUDGET", null, false));
    }

    private static Observation Counted(string state, DateTimeOffset asOf) => new(Guid.NewGuid(), ObservationKind.Current, asOf, ProjectionReadings.State(state));
}
