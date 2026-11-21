using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>
/// The Portfolio designer's data catalogue over FG-02's report rows (ADR-003 §8.2 edge 47, Dashboards → Project; the 18 Nov 2026 data fields): every
/// field of the register's project-grain and snapshot-grain projections, read through <see cref="IProjectionRowReader"/>, so each cell is authorised on its
/// own projection's permission and masked as a report cell is (FG-02 §8.2). Nothing here reads a table or computes a value a source owns.
/// </summary>
internal sealed class PortfolioFieldReader(IProjectionRowReader rows) : IPortfolioFieldReader
{
    public IReadOnlyList<PortfolioFieldDescriptor> Fields { get; } = [.. Catalogue(rows.Projections)];

    private Dictionary<string, PortfolioFieldDescriptor> ByKey => field ??= Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>>> ReadAsync(
        Guid callerId, IReadOnlyCollection<Guid> projectIds, IReadOnlyCollection<string> fieldKeys, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        ArgumentNullException.ThrowIfNull(fieldKeys);
        List<PortfolioFieldDescriptor> fields = [.. fieldKeys.Distinct(StringComparer.Ordinal).Select(k => ByKey.GetValueOrDefault(k)).OfType<PortfolioFieldDescriptor>()];
        Dictionary<Guid, Dictionary<string, List<PortfolioFieldCell>>> cells = [];
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>>();
        }

        // A project-grain row presents project-grain projections on PROJECT.LIFECYCLE_STATE; a snapshot-grain row only its own projection, on itself.
        foreach (IGrouping<string, PortfolioFieldDescriptor> read in fields.GroupBy(f => f.Grain == PortfolioFieldGrain.Project ? DashboardProjections.ProjectLifecycleState : f.ProjectionCode))
        {
            List<PortfolioFieldDescriptor> columns = [.. read];
            ProjectionRowSet set = await rows.ReadAsync(
                new ProjectionRowQuery(
                    callerId, read.Key, [], [.. columns.Select(f => new ProjectionColumn(f.ProjectionCode, f.FieldCode, null))], projectIds,
                    DepartmentId: null, MaskSensitiveFields: false, now),
                cancellationToken).ConfigureAwait(false);
            foreach (ProjectionRow row in set.Rows)
            {
                Dictionary<string, List<PortfolioFieldCell>> project = cells.TryGetValue(row.ProjectId, out Dictionary<string, List<PortfolioFieldCell>>? known)
                    ? known
                    : cells[row.ProjectId] = new(StringComparer.Ordinal);
                for (int index = 0; index < columns.Count; index++)
                {
                    ProjectionCell cell = row.Cells[index];
                    (project.TryGetValue(columns[index].Key, out List<PortfolioFieldCell>? list) ? list : project[columns[index].Key] = [])
                        .Add(new PortfolioFieldCell(cell.IsRevealed ? cell.Value : null, row.SnapshotIndex));
                }
            }
        }

        return cells.ToDictionary(
            c => c.Key,
            IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>> (c) => c.Value.ToDictionary(k => k.Key, IReadOnlyList<PortfolioFieldCell> (k) => k.Value, StringComparer.Ordinal));
    }

    /// <summary>The register's project-grain projections' fields, then its snapshot-grain projections' fields, each in the register's order.</summary>
    internal static IEnumerable<PortfolioFieldDescriptor> Catalogue(IEnumerable<ProjectionDescriptor> projections) =>
        from projection in projections.OrderBy(p => p.Grain == ProjectionGrain.Project ? 0 : 1)
        from field in projection.Fields
        let type = TypeOf(field.Type)
        let grain = projection.Grain == ProjectionGrain.Project ? PortfolioFieldGrain.Project : PortfolioFieldGrain.Snapshot
        select new PortfolioFieldDescriptor(
            $"{projection.Code}.{field.Code}", projection.Code, projection.SourceDomain, field.Code, type, field.IsSensitive, field.Values,
            PortfolioFieldDescriptor.AggregationsOf(type), grain);

    private static PortfolioFieldType TypeOf(ProjectionValueType type) => type switch
    {
        ProjectionValueType.Code => PortfolioFieldType.Code,
        ProjectionValueType.Count => PortfolioFieldType.Count,
        ProjectionValueType.Percent => PortfolioFieldType.Percent,
        ProjectionValueType.Days => PortfolioFieldType.Days,
        ProjectionValueType.Sar => PortfolioFieldType.Sar,
        ProjectionValueType.Boolean => PortfolioFieldType.Boolean,
        ProjectionValueType.Date => PortfolioFieldType.Date,
        ProjectionValueType.DateTime => PortfolioFieldType.DateTime,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown value type."),
    };
}
