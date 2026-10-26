using System.Globalization;
using PMPlatform.Application.Common.Projections;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>What a projection says about one widget, before the composition adds the widget's own fields.</summary>
internal sealed record ProjectionReading(
    ProjectionFreshness Freshness,
    WidgetUnknownReason? UnknownReason,
    DateTimeOffset? AsOf,
    ProjectionCoverage Coverage,
    WidgetData? Data,
    WidgetCoverage? CoverageDetail,
    IReadOnlyList<string> MaskedFields)
{
    public static ProjectionReading Unknown(WidgetUnknownReason reason, WidgetCoverage? coverage = null) =>
        new(ProjectionFreshness.Unknown, reason, null, ProjectionCoverage.None, null, coverage, []);
}

/// <summary>How one project's source value stands (FG-01 §7.3).</summary>
internal enum ObservationKind
{
    /// <summary>A value the source says is current.</summary>
    Current = 1,

    /// <summary>A value the source says is out of date: shown, labelled STALE, with its own as-of (BR-DSH-041).</summary>
    Stale = 2,

    /// <summary>No value: never 0 (BR-DSH-013).</summary>
    Missing = 3,

    /// <summary>The value does not apply: outside the denominator (BR-DSH-016).</summary>
    NotApplicable = 4,

    /// <summary>The value is withheld from the caller's audience (ADR-010).</summary>
    Masked = 5,

    /// <summary>The value cannot be combined with the others (a currency not verified as SAR).</summary>
    Incompatible = 6,
}

/// <summary>One project's value of a projection, as its source states it.</summary>
internal sealed record Observation(Guid ProjectId, ObservationKind Kind, DateTimeOffset? AsOf, WidgetData? Data, IReadOnlyList<string>? MaskedFields = null)
{
    public bool IsCounted => Kind is ObservationKind.Current or ObservationKind.Stale;
}

/// <summary>
/// The one place a projection's observations become a widget's reading, so every widget follows FG-01 §7 the same way: an absent
/// value is UNKNOWN with its reason and no data; a STALE one keeps its value; an aggregate counts only what it may, says what it left
/// out, is STALE if any counted value is, and is dated by its least current counted value. Nothing here computes a business value:
/// it counts source-owned states and passes source-owned figures through.
/// </summary>
internal static class ProjectionReadings
{
    public static readonly IReadOnlyList<WidgetFigure> NoFigures = [];

    public static readonly IReadOnlyList<WidgetBucket> NoBuckets = [];

    public static readonly IReadOnlyList<WidgetSeriesPoint> NoSeries = [];

    /// <summary>One project's reading (the Project Dashboard).</summary>
    public static ProjectionReading Of(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        IReadOnlyList<string> masked = observation.MaskedFields ?? [];
        return observation.Kind switch
        {
            ObservationKind.Current => new(ProjectionFreshness.Fresh, null, observation.AsOf, ProjectionCoverage.Complete, observation.Data, null, masked),
            ObservationKind.Stale => new(ProjectionFreshness.Stale, null, observation.AsOf, ProjectionCoverage.Complete, observation.Data, null, masked),
            ObservationKind.NotApplicable => ProjectionReading.Unknown(WidgetUnknownReason.NotApplicable),
            ObservationKind.Masked => ProjectionReading.Unknown(WidgetUnknownReason.Restricted) with { MaskedFields = masked },
            ObservationKind.Missing or ObservationKind.Incompatible => ProjectionReading.Unknown(WidgetUnknownReason.Missing),
            _ => throw new ArgumentOutOfRangeException(nameof(observation), observation.Kind, "Unknown observation kind."),
        };
    }

    /// <summary>
    /// A population's reading: <paramref name="combine"/> receives the counted observations only. A project whose value does not
    /// apply is outside the denominator; one missing, masked or incompatible is excluded and counted as such.
    /// </summary>
    public static ProjectionReading Aggregate(IReadOnlyList<Observation> observations, Func<IReadOnlyList<Observation>, WidgetData> combine)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(combine);

        List<Observation> eligible = [.. observations.Where(o => o.Kind != ObservationKind.NotApplicable)];
        List<Observation> counted = [.. eligible.Where(o => o.IsCounted)];
        List<CoverageExclusion> exclusions =
        [
            .. eligible.Where(o => !o.IsCounted)
                .GroupBy(o => o.Kind == ObservationKind.Masked ? CoverageExclusionReason.Masked
                    : o.Kind == ObservationKind.Incompatible ? CoverageExclusionReason.Incompatible
                    : CoverageExclusionReason.Missing)
                .OrderBy(g => g.Key)
                .Select(g => new CoverageExclusion(g.Key, g.Count())),
        ];
        int stale = counted.Count(o => o.Kind == ObservationKind.Stale);
        WidgetCoverage coverage = new(eligible.Count, counted.Count, eligible.Count - counted.Count, stale, exclusions);
        IReadOnlyList<string> masked = [.. observations.SelectMany(o => o.MaskedFields ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

        if (eligible.Count == 0)
        {
            return ProjectionReading.Unknown(WidgetUnknownReason.NotApplicable, coverage);
        }

        if (counted.Count == 0)
        {
            WidgetUnknownReason reason = eligible.All(o => o.Kind == ObservationKind.Masked) ? WidgetUnknownReason.Restricted : WidgetUnknownReason.Missing;
            return ProjectionReading.Unknown(reason, coverage) with { MaskedFields = masked };
        }

        return new(
            stale > 0 ? ProjectionFreshness.Stale : ProjectionFreshness.Fresh,
            null,
            counted.Min(o => o.AsOf),
            counted.Count == eligible.Count ? ProjectionCoverage.Complete : ProjectionCoverage.Partial,
            combine(counted),
            coverage,
            masked);
    }

    /// <summary>A count of the counted observations by their source-owned state, in the source's order of states.</summary>
    public static WidgetData Distribution(IReadOnlyList<Observation> counted, IReadOnlyList<string> order) =>
        Buckets(counted.Select(o => o.Data?.State).OfType<string>().GroupBy(s => s, StringComparer.Ordinal).Select(g => new WidgetBucket(g.Key, null, g.Count())), order);

    /// <summary>The counted observations' buckets added key by key: counts are additive across projects (FG-01 §8).</summary>
    public static WidgetData SumBuckets(IReadOnlyList<Observation> counted, IReadOnlyList<string> order) =>
        Buckets(
            counted.SelectMany(o => o.Data?.Distribution ?? NoBuckets).GroupBy(b => b.Key, StringComparer.Ordinal)
                .Select(g => new WidgetBucket(g.Key, g.First().Label, g.Sum(b => b.Count))),
            order,
            SumFigures(counted));

    /// <summary>A single value: a source-owned state and figures.</summary>
    public static WidgetData State(string? state, params WidgetFigure[] figures) => new(state, figures, NoBuckets, NoSeries);

    public static WidgetFigure Count(string measure, int count) => new(measure, count.ToString(CultureInfo.InvariantCulture), "COUNT", false);

    public static WidgetFigure Percent(string measure, decimal? value) => new(measure, value?.ToString(CultureInfo.InvariantCulture), "PERCENT", false);

    public static WidgetFigure Days(string measure, int? value) => new(measure, value?.ToString(CultureInfo.InvariantCulture), "DAYS", false);

    /// <summary>R-16: money as its exact two-place decimal string, in SAR; a masked amount has no value.</summary>
    public static WidgetFigure Sar(string measure, Money? amount, bool masked) =>
        new(measure, masked ? null : amount?.Amount.ToString("0.00", CultureInfo.InvariantCulture), Money.CurrencyCode, masked);

    /// <summary>The enum's members as R-19 names them, in declaration order: a source's own order of states.</summary>
    public static IReadOnlyList<string> Names<TEnum>()
        where TEnum : struct, Enum =>
        [.. Enum.GetValues<TEnum>().Select(Name)];

    public static string Name<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    private static WidgetData Buckets(IEnumerable<WidgetBucket> buckets, IReadOnlyList<string> order, IReadOnlyList<WidgetFigure>? figures = null)
    {
        Dictionary<string, int> rank = order.Select((key, index) => (key, index)).ToDictionary(x => x.key, x => x.index, StringComparer.Ordinal);
        return new(null, figures ?? NoFigures, [.. buckets.OrderBy(b => rank.GetValueOrDefault(b.Key, int.MaxValue)).ThenBy(b => b.Key, StringComparer.Ordinal)], NoSeries);
    }

    /// <summary>COUNT figures, the only kind summed across projects; any other unit is not additive here and is not combined.</summary>
    private static IReadOnlyList<WidgetFigure> SumFigures(IReadOnlyList<Observation> counted) =>
        [.. counted.SelectMany(o => o.Data?.Figures ?? NoFigures).Where(f => f.Unit == "COUNT" && f.Value is not null)
            .GroupBy(f => f.Measure, StringComparer.Ordinal)
            .Select(g => Count(g.Key, g.Sum(f => int.Parse(f.Value!, CultureInfo.InvariantCulture))))];
}
