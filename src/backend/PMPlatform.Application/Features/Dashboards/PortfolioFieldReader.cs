using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>
/// The Portfolio designer's data catalogue over FG-02's report rows (ADR-003 §8.2 edge 47, Dashboards → Project; the 18 Nov 2026 data fields): every
/// project-grain field of the register a visual can calculate with, read through <see cref="IProjectionRowReader"/>, so each cell is authorised on its own
/// projection's permission and masked as a report cell is (FG-02 §8.2). Nothing here reads a table or computes a value a source owns.
/// </summary>
internal sealed class PortfolioFieldReader(IProjectionRowReader rows) : IPortfolioFieldReader
{
    public IReadOnlyList<PortfolioFieldDescriptor> Fields { get; } = [.. Catalogue(rows.Projections)];

    private Dictionary<string, PortfolioFieldDescriptor> ByKey => field ??= Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, PortfolioFieldCell>>> ReadAsync(
        Guid callerId, IReadOnlyCollection<Guid> projectIds, IReadOnlyCollection<string> fieldKeys, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        ArgumentNullException.ThrowIfNull(fieldKeys);
        List<PortfolioFieldDescriptor> fields = [.. fieldKeys.Distinct(StringComparer.Ordinal).Select(k => ByKey.GetValueOrDefault(k)).OfType<PortfolioFieldDescriptor>()];
        if (fields.Count == 0 || projectIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyDictionary<string, PortfolioFieldCell>>();
        }

        ProjectionRowSet set = await rows.ReadAsync(
            new ProjectionRowQuery(
                callerId, DashboardProjections.ProjectLifecycleState, [], [.. fields.Select(f => new ProjectionColumn(f.ProjectionCode, f.FieldCode, null))], projectIds,
                DepartmentId: null, MaskSensitiveFields: false, now),
            cancellationToken).ConfigureAwait(false);
        return set.Rows.ToDictionary(
            r => r.ProjectId,
            IReadOnlyDictionary<string, PortfolioFieldCell> (r) => fields
                .Select((f, index) => (f.Key, Cell: new PortfolioFieldCell(r.Cells[index].IsRevealed ? r.Cells[index].Value : null)))
                .ToDictionary(c => c.Key, c => c.Cell, StringComparer.Ordinal));
    }

    /// <summary>The register's project-grain projections' fields of a type a visual calculates with, in the register's order.</summary>
    internal static IEnumerable<PortfolioFieldDescriptor> Catalogue(IEnumerable<ProjectionDescriptor> projections) =>
        from projection in projections
        where projection.Grain == ProjectionGrain.Project
        from field in projection.Fields
        let type = TypeOf(field.Type)
        where type is not null
        select new PortfolioFieldDescriptor(
            $"{projection.Code}.{field.Code}", projection.Code, projection.SourceDomain, field.Code, type.Value, field.IsSensitive, field.Values,
            PortfolioFieldDescriptor.AggregationsOf(type.Value));

    private static PortfolioFieldType? TypeOf(ProjectionValueType type) => type switch
    {
        ProjectionValueType.Code => PortfolioFieldType.Code,
        ProjectionValueType.Count => PortfolioFieldType.Count,
        ProjectionValueType.Percent => PortfolioFieldType.Percent,
        ProjectionValueType.Days => PortfolioFieldType.Days,
        ProjectionValueType.Sar => PortfolioFieldType.Sar,
        ProjectionValueType.Boolean or ProjectionValueType.Date or ProjectionValueType.DateTime => null,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown value type."),
    };
}
