using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Projections;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Dashboards.Projections;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>
/// FG-02's rows over FG-01's register (ADR-003 §8.2 edge 30; TASK-071). The population and every cell are authorised exactly as a dashboard
/// widget is: the projects the caller's record scope reaches under the projection's source permission, with the column's classification
/// cleared, decided by the authorization engine before any source is read (FG-02 §8.2). Each projection is read once, through its adapter,
/// for the projects it may show; one that fails leaves its cells SOURCE_UNAVAILABLE and the rest of the row stands (FG-02 §26). Nothing here
/// computes a value a source owns.
/// </summary>
internal sealed partial class ProjectionRowReader(
    IProjectFactsReader projects,
    IAuthorizationEngine engine,
    IEnumerable<IDashboardProjectionSource> sources,
    ILogger<ProjectionRowReader> logger) : IProjectionRowReader
{
    private readonly Dictionary<string, IDashboardProjectionSource> _sources = ProjectionSources.ByCode(sources);

    public IReadOnlyList<ProjectionDescriptor> Projections { get; } = [.. DashboardProjections.Reportable.Select(p => p.ToDescriptor())];

    public async Task<ProjectionRowSet> ReadAsync(ProjectionRowQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ProjectionContract basis = Reportable(query.BaseProjectionCode);
        List<Column> columns = [.. query.Columns.Select(c => Column.Of(Reportable(c.ProjectionCode), c))];
        if (columns.Any(c => basis.Grain == ProjectionGrain.Snapshot ? c.Projection != basis : c.Projection.Grain != ProjectionGrain.Project))
        {
            throw new ArgumentException("A snapshot-grain row presents its own projection only; a project-grain row, project-grain projections only.", nameof(query));
        }

        List<ProjectionContract> read = [basis, .. columns.Select(c => c.Projection).Where(p => p != basis).Distinct()];
        Dictionary<string, RecordScope> scopes = [];
        foreach (string permission in read.Select(p => p.PermissionCode).Concat(query.RowPermissionCodes).Distinct(StringComparer.Ordinal))
        {
            scopes[permission] = await engine.GetRecordScopeAsync(query.CallerId, permission, cancellationToken).ConfigureAwait(false);
        }

        // The population: what the base projection's permission reaches, narrowed by every row permission and the query's own filters.
        RecordScope reach = scopes[basis.PermissionCode];
        IReadOnlyList<ProjectFacts> reached = reach.IsEmpty ? [] : await projects.ListReachedAsync(reach, cancellationToken).ConfigureAwait(false);
        List<ProjectFacts> population = [.. reached.Where(p => (query.IncludeIneligible || basis.IsEligible(p.Status))
            && query.RowPermissionCodes.All(permission => scopes[permission].Matches(ProjectSubjects.Of(p, null)))
            && (query.ProjectIds is null || query.ProjectIds.Contains(p.Id)))];
        List<Guid> departments = [.. population.Select(p => p.DepartmentId).Distinct().Order()];
        bool offered = query.DepartmentId is not { } departmentId || departments.Contains(departmentId);
        if (query.DepartmentId is { } department)
        {
            population = offered ? [.. population.Where(p => p.DepartmentId == department)] : [];
        }

        // Each cell's own authorization: the projection's permission and the column's classification (the base reaches every row unclassified).
        Dictionary<(string Projection, Guid? Classification), HashSet<Guid>> cleared = [];
        foreach ((ProjectionContract projection, Guid? classification) in columns.Select(c => (c.Projection, c.ClassificationId)).Append((basis, null)).Distinct())
        {
            cleared[(projection.Code, classification)] =
                [.. population.Where(p => scopes[projection.PermissionCode].Matches(ProjectSubjects.Of(p, classification))).Select(p => p.Id)];
        }

        Dictionary<string, IReadOnlyDictionary<Guid, Observation>?> observed = [];
        foreach (ProjectionContract projection in read)
        {
            List<ProjectFacts> shown = [.. population.Where(p => Reaches(cleared, projection, p.Id) && projection.IsEligible(p.Status))];
            observed[projection.Code] = await ObserveAsync(projection, query, shown, cancellationToken).ConfigureAwait(false);
        }

        List<ProjectionRow> rows = [];
        foreach (ProjectFacts project in population)
        {
            if (basis.Grain == ProjectionGrain.Project)
            {
                rows.Add(Row(project, null, [.. columns.Select(c => Cell(c, project, cleared, observed, query.MaskSensitiveFields))]));
            }
            else if (observed[basis.Code]?.GetValueOrDefault(project.Id) is { IsCounted: true, Data: { } history } snapshots)
            {
                rows.AddRange(history.Series.Select((point, index) =>
                    Row(project, index, [.. columns.Select(c => Cell(c.Field.Read(point), c, snapshots.Kind, point.AsOf, query.MaskSensitiveFields))])));
            }
        }

        return new ProjectionRowSet(offered, departments, rows, [.. read.Select(p => Section(p, population, cleared, observed))]);
    }

    private static ProjectionContract Reportable(string code) =>
        DashboardProjections.Find(code) is { Grain: not null } projection
            ? projection
            : throw new ArgumentException($"{code} is not a registered projection about projects.", nameof(code));

    private static bool Reaches(Dictionary<(string Projection, Guid? Classification), HashSet<Guid>> cleared, ProjectionContract projection, Guid projectId) =>
        cleared.Any(entry => entry.Key.Projection == projection.Code && entry.Value.Contains(projectId));

    private static ProjectionRow Row(ProjectFacts project, int? snapshotIndex, IReadOnlyList<ProjectionCell> cells) =>
        new(project.Id, project.FormalProjectId, project.Title, project.DepartmentId, project.ExternalEntityId, snapshotIndex, cells);

    /// <summary>A project-grain cell: restricted unless the column's permission and classification reach the project; then the source's word.</summary>
    private static ProjectionCell Cell(
        Column column, ProjectFacts project, Dictionary<(string Projection, Guid? Classification), HashSet<Guid>> cleared,
        Dictionary<string, IReadOnlyDictionary<Guid, Observation>?> observed, bool maskSensitive)
    {
        if (!cleared[(column.Projection.Code, column.ClassificationId)].Contains(project.Id))
        {
            return ProjectionCell.Unknown(WidgetUnknownReason.Restricted);
        }

        if (!column.Projection.IsEligible(project.Status))
        {
            return ProjectionCell.Unknown(WidgetUnknownReason.NotApplicable);
        }

        if (observed[column.Projection.Code] is not { } values)
        {
            return ProjectionCell.Unknown(WidgetUnknownReason.SourceUnavailable);
        }

        Observation observation = values[project.Id];
        return observation.Kind switch
        {
            ObservationKind.Current or ObservationKind.Stale => Cell(column.Field.Read(observation.Data!), column, observation.Kind, observation.AsOf, maskSensitive),
            ObservationKind.NotApplicable => ProjectionCell.Unknown(WidgetUnknownReason.NotApplicable),
            ObservationKind.Masked => ProjectionCell.Unknown(WidgetUnknownReason.Restricted, isMasked: true),
            ObservationKind.Missing or ObservationKind.Incompatible => ProjectionCell.Unknown(WidgetUnknownReason.Missing),
            _ => throw new ArgumentOutOfRangeException(nameof(observed), observation.Kind, "Unknown observation kind."),
        };
    }

    /// <summary>A value the source stated: withheld if the source masked it or the query masks sensitive fields (ADR-010, ADR-013); absent is MISSING, never 0.</summary>
    private static ProjectionCell Cell((string? Value, bool IsMasked) read, Column column, ObservationKind kind, DateTimeOffset? asOf, bool maskSensitive) =>
        read.IsMasked || (maskSensitive && column.Field.IsSensitive) ? ProjectionCell.Unknown(WidgetUnknownReason.Restricted, isMasked: true)
        : read.Value is null ? ProjectionCell.Unknown(WidgetUnknownReason.Missing)
        : new ProjectionCell(read.Value, kind == ObservationKind.Stale ? ProjectionFreshness.Stale : ProjectionFreshness.Fresh, asOf, null, false);

    /// <summary>
    /// The projection's banner over the population (FG-02 §7.2): a project it may not show is excluded as restricted, one it expects no value from
    /// is outside the denominator, and the rest are counted as the source stated them — by the same rule as a dashboard aggregate.
    /// </summary>
    private static ProjectionSection Section(
        ProjectionContract projection, IReadOnlyList<ProjectFacts> population, Dictionary<(string Projection, Guid? Classification), HashSet<Guid>> cleared,
        Dictionary<string, IReadOnlyDictionary<Guid, Observation>?> observed)
    {
        ProjectionReading reading;
        if (observed[projection.Code] is not { } values)
        {
            reading = ProjectionReading.Unknown(WidgetUnknownReason.SourceUnavailable);
        }
        else
        {
            List<Observation> observations = [.. population.Select(p =>
                !Reaches(cleared, projection, p.Id) ? new Observation(p.Id, ObservationKind.Masked, null, null)
                : !projection.IsEligible(p.Status) ? new Observation(p.Id, ObservationKind.NotApplicable, null, null)
                : values[p.Id])];
            reading = ProjectionReadings.Aggregate(observations, _ => new WidgetData(null, ProjectionReadings.NoFigures, ProjectionReadings.NoBuckets, ProjectionReadings.NoSeries));
        }

        return new ProjectionSection(
            projection.Code, projection.SourceDomain, projection.Version, new ProjectionMeta(projection.SemanticState, reading.Freshness, reading.AsOf, reading.Coverage),
            reading.UnknownReason, reading.CoverageDetail ?? new WidgetCoverage(0, 0, 0, 0, []));
    }

    /// <summary>The projection's values for the projects it may show, by project; null when its source failed (logged, never its data).</summary>
    private async Task<IReadOnlyDictionary<Guid, Observation>?> ObserveAsync(
        ProjectionContract projection, ProjectionRowQuery query, List<ProjectFacts> shown, CancellationToken cancellationToken)
    {
        if (shown.Count == 0)
        {
            return new Dictionary<Guid, Observation>();
        }

        if (!_sources.TryGetValue(projection.Code, out IDashboardProjectionSource? source))
        {
            return null;
        }

        try
        {
            IReadOnlyList<Observation> observations = await source.ObserveAsync(
                new ProjectionRequest(query.CallerId, DashboardContextKind.Portfolio, shown, query.Now), cancellationToken).ConfigureAwait(false);
            return observations.ToDictionary(o => o.ProjectId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSourceUnavailable(logger, projection.Code, exception.GetType().Name);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report rows: projection {ProjectionCode} is unavailable ({Reason}); its cells answer SOURCE_UNAVAILABLE.")]
    private static partial void LogSourceUnavailable(ILogger logger, string projectionCode, string reason);

    private sealed record Column(ProjectionContract Projection, ProjectionField Field, Guid? ClassificationId)
    {
        public static Column Of(ProjectionContract projection, ProjectionColumn column) =>
            new(projection, projection.Field(column.FieldCode) ?? throw new ArgumentException($"{column.ProjectionCode} has no field {column.FieldCode}.", nameof(column)),
                column.DataClassificationItemId);
    }
}
