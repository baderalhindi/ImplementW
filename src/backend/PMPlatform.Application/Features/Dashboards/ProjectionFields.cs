using System.Globalization;
using PMPlatform.Application.Features.Dashboards.Contracts;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>Where in a projection's own value a field is read from.</summary>
internal enum ProjectionFieldSource
{
    /// <summary>The value's source-owned state (<see cref="WidgetData.State"/>).</summary>
    State = 1,

    /// <summary>A measure among <see cref="WidgetData.Figures"/>, by its key.</summary>
    Figure = 2,

    /// <summary>A count among <see cref="WidgetData.Distribution"/>, by its key; an absent bucket is the source's zero.</summary>
    Bucket = 3,

    /// <summary>A snapshot's as-of (<see cref="WidgetSeriesPoint.AsOf"/>).</summary>
    SnapshotAsOf = 4,

    /// <summary>A snapshot's period start.</summary>
    SnapshotPeriodStart = 5,

    /// <summary>A snapshot's period end.</summary>
    SnapshotPeriodEnd = 6,

    /// <summary>A snapshot's state.</summary>
    SnapshotState = 7,

    /// <summary>A measure of a snapshot, by its key.</summary>
    SnapshotFigure = 8,
}

/// <summary>
/// A field of a registered projection: what a report column may present of it (FG-02 §4.1). It is read from the projection's own value —
/// the observation an adapter states for one project — and never computed: a field is a state, a measure or a count the source made.
/// </summary>
internal sealed record ProjectionField(string Code, ProjectionValueType Type, ProjectionFieldSource Source, string? Key, bool IsSensitive, IReadOnlyList<string> Values)
{
    public ProjectionFieldDescriptor ToDescriptor() => new(Code, Type, IsSensitive, Values);

    /// <summary>A project-grain value, and whether the source withheld it from the caller's audience (ADR-010).</summary>
    public (string? Value, bool IsMasked) Read(WidgetData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Source switch
        {
            ProjectionFieldSource.State => (data.State, false),
            ProjectionFieldSource.Figure => data.Figures.FirstOrDefault(f => f.Measure == Key) is { } figure ? (figure.Value, figure.IsMasked) : (null, false),
            ProjectionFieldSource.Bucket => ((data.Distribution.FirstOrDefault(b => b.Key == Key)?.Count ?? 0).ToString(CultureInfo.InvariantCulture), false),
            ProjectionFieldSource.SnapshotAsOf or ProjectionFieldSource.SnapshotPeriodStart or ProjectionFieldSource.SnapshotPeriodEnd
                or ProjectionFieldSource.SnapshotState or ProjectionFieldSource.SnapshotFigure =>
                throw new InvalidOperationException($"{Code} is read from a snapshot, not a project's value."),
            _ => throw new InvalidOperationException($"{Code} has an unknown source."),
        };
    }

    /// <summary>A snapshot-grain value: one point of the source's own history.</summary>
    public (string? Value, bool IsMasked) Read(WidgetSeriesPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return Source switch
        {
            ProjectionFieldSource.SnapshotAsOf => (point.AsOf.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture), false),
            ProjectionFieldSource.SnapshotPeriodStart => (point.PeriodStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), false),
            ProjectionFieldSource.SnapshotPeriodEnd => (point.PeriodEnd?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), false),
            ProjectionFieldSource.SnapshotState => (point.State, false),
            ProjectionFieldSource.SnapshotFigure => point.Figures.FirstOrDefault(f => f.Measure == Key) is { } figure ? (figure.Value, figure.IsMasked) : (null, false),
            ProjectionFieldSource.State or ProjectionFieldSource.Figure or ProjectionFieldSource.Bucket =>
                throw new InvalidOperationException($"{Code} is read from a project's value, not a snapshot."),
            _ => throw new InvalidOperationException($"{Code} has an unknown source."),
        };
    }

    public static ProjectionField State(string code, IReadOnlyList<string> values) => new(code, ProjectionValueType.Code, ProjectionFieldSource.State, null, false, values);

    public static ProjectionField Figure(string code, ProjectionValueType type, bool isSensitive = false) => new(code, type, ProjectionFieldSource.Figure, code, isSensitive, []);

    public static ProjectionField Bucket(string code, string key) => new(code, ProjectionValueType.Count, ProjectionFieldSource.Bucket, key, false, []);

    public static ProjectionField Snapshot(string code, ProjectionValueType type, ProjectionFieldSource source, IReadOnlyList<string>? values = null) =>
        new(code, type, source, source == ProjectionFieldSource.SnapshotFigure ? code : null, false, values ?? []);
}
