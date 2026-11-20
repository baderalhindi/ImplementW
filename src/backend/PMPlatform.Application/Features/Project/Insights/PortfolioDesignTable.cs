using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Insights;

/// <summary>What a column of a TABLE visual reads (the 20 Nov 2026 Power BI-style table): a project attribute, a data catalogue field or a portfolio figure.</summary>
public enum PortfolioTableColumnKind
{
    /// <summary>A project attribute: a <see cref="PortfolioGroupBy"/> dimension, <c>TITLE</c> or <c>FORMAL_PROJECT_ID</c>.</summary>
    Dimension = 1,

    /// <summary>A project-grain field of the data catalogue (<see cref="PortfolioFieldDescriptor.Key"/>).</summary>
    Field = 2,

    /// <summary>A portfolio figure, calculated over the row's projects as the insights calculate it for a group.</summary>
    Measure = 3,
}

/// <summary>How a table column is calculated within a row; <see cref="None"/> ("Don't summarize") groups the rows by the column's value.</summary>
public enum PortfolioTableAggregation
{
    None = 1,
    Count = 2,
    CountDistinct = 3,
    Sum = 4,
    Average = 5,
    Min = 6,
    Max = 7,
    CountTrue = 8,
    ShareTrue = 9,
}

/// <summary>How a table column's cells are formatted.</summary>
public enum PortfolioTableColumnType
{
    /// <summary>An item, department or entity: the key is its id, the label its name.</summary>
    Item = 1,
    Code = 2,
    Text = 3,
    Count = 4,
    Percent = 5,
    Days = 6,
    Sar = 7,

    /// <summary>A day number since 1970-01-01, as a date field's values are.</summary>
    Date = 8,
    Boolean = 9,
    OnPlan = 10,
    Points = 11,
}

/// <summary>A column of a TABLE visual: what it reads and its calculation (null for a <see cref="PortfolioTableColumnKind.Measure"/>).</summary>
public sealed record PortfolioTableColumn(PortfolioTableColumnKind Kind, string Key, PortfolioTableAggregation? Aggregation);

/// <summary>
/// One cell: a grouped column's <see cref="Key"/> (an id, a code, a date or a number as written) and <see cref="Label"/> when the backend names it, or a
/// calculated <see cref="Value"/>. A value the caller may not see is null with <see cref="Masked"/> true; a missing one is null, never 0, except a count.
/// </summary>
public sealed record PortfolioTableCell(string? Key, BilingualLabel? Label, decimal? Value, bool Masked = false);

/// <summary>A row: its projects, the project when the row is one project (grouped by its title or ID), and a cell per column in order.</summary>
public sealed record PortfolioTableRow(int Projects, Guid? ProjectId, IReadOnlyList<PortfolioTableCell> Cells);

/// <summary>A column table's figures: a type per column, the rows (at most 500, <see cref="Truncated"/> beyond), and the totals unless switched off.</summary>
public sealed record PortfolioTableResult(
    IReadOnlyList<PortfolioTableColumnType> ColumnTypes, IReadOnlyList<PortfolioTableRow> Rows, IReadOnlyList<PortfolioTableCell>? Totals, bool Truncated);

/// <summary>The Power BI-style table (dashboard-designer-power-table.md): its rules and its calculation.</summary>
internal sealed partial class PortfolioDesignService
{
    public const int MaxTableColumns = 12;
    public const int MaxTableRows = 500;
    private const string TitleKey = "TITLE";
    private const string FormalProjectIdKey = "FORMAL_PROJECT_ID";

    /// <summary>The project attributes a column takes: each grouping dimension by its code (<c>SECTOR</c>, …), then the title and the formal ID (null: one per project).</summary>
    private static readonly Dictionary<string, PortfolioGroupBy?> ColumnDimensions = Enum.GetValues<PortfolioGroupBy>()
        .Where(g => g != PortfolioGroupBy.None).Select(g => (Code(g), (PortfolioGroupBy?)g))
        .Append((TitleKey, null)).Append((FormalProjectIdKey, null))
        .ToDictionary(d => d.Item1, d => d.Item2, StringComparer.Ordinal);

    /// <summary>The portfolio figures a column takes: the fixed measures by their code, then MEASURED, ON_PLAN and PROJECT_SHARE, then the box sections' figures.</summary>
    private static readonly Dictionary<string, TableMeasure> ColumnMeasures = Enum.GetValues<TableMeasure>().ToDictionary(m => Code(m), StringComparer.Ordinal);

    private static bool IsColumnTable(PortfolioVisualInput v) => v.VisualType == PortfolioVisualType.Table && v.Columns is { Count: > 0 };

    /// <summary>
    /// The permissions a visual's projects are read under: its grouping's; for a column table, each dimension column's grouping's (the title and the ID as
    /// an ungrouped visual's), or an ungrouped visual's when it names none; then each figure's own (<see cref="PermissionOf(PortfolioMeasure)"/>, the 20 Nov
    /// 2026 box sections). An ungrouped visual whose every figure has its own permission is read under those alone, as the section its figures are boxes of.
    /// A visual shows only the projects every one of them reaches.
    /// </summary>
    internal static IReadOnlyList<string> PermissionsOf(PortfolioVisualInput v)
    {
        List<string?> own;
        List<string> grouping;
        if (IsColumnTable(v))
        {
            own = [.. v.Columns!.Where(c => c.Kind == PortfolioTableColumnKind.Measure)
                .Select(c => ColumnMeasures.TryGetValue(c.Key, out TableMeasure m) && FigureOf(m) is { } figure ? PermissionOf(figure) : null)];
            grouping = [.. v.Columns!.Where(c => c.Kind == PortfolioTableColumnKind.Dimension)
                .Select(c => PermissionOf(ColumnDimensions.GetValueOrDefault(c.Key) ?? PortfolioGroupBy.None))];
            bool ownOnly = grouping.Count == 0 && own.Count == v.Columns!.Count && own.TrueForAll(p => p is not null);
            if (grouping.Count == 0 && !ownOnly)
            {
                grouping = [PermissionOf(PortfolioGroupBy.None)];
            }
        }
        else
        {
            own = [.. (v.Measures ?? []).Select(PermissionOf)];
            bool ownOnly = v.GroupBy == PortfolioGroupBy.None && own.Count > 0 && own.TrueForAll(p => p is not null);
            grouping = ownOnly ? [] : [PermissionOf(v.GroupBy)];
        }

        return [.. grouping.Concat(own.OfType<string>()).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// What is wrong with a column table, or null (its field relative to the visual): 1 to 12 columns, each a known key of its kind with a calculation it takes
    /// — a dimension none, a count or a distinct count; a field none, a distinct count or its type's own; a measure none at all — no two alike; and the visual's
    /// own measures, values, grouping and grouping field left empty.
    /// </summary>
    private static FieldIssue? ColumnsIssue(PortfolioVisualInput v, IReadOnlyList<PortfolioTableColumn> columns, Dictionary<string, PortfolioFieldDescriptor> catalogue)
    {
        if (columns.Count > MaxTableColumns)
        {
            return new("columns", FieldIssue.OutsideRange);
        }

        for (int index = 0; index < columns.Count; index++)
        {
            if (ColumnIssue(columns[index], catalogue) is { } issue)
            {
                return new($"columns[{index}]{issue.Field}", issue.Code);
            }

            if (columns.Take(index).Contains(columns[index]))
            {
                return new($"columns[{index}]", FieldIssue.Duplicate);
            }
        }

        return (v.Measures?.Count ?? 0) != 0 ? new("measures", FieldIssue.NotAllowed)
            : (v.Values?.Count ?? 0) != 0 ? new("values", FieldIssue.NotAllowed)
            : v.GroupBy != PortfolioGroupBy.None ? new("groupBy", FieldIssue.NotAllowed)
            : v.GroupField is not null ? new("groupField", FieldIssue.NotAllowed)
            : v.GroupDateUnit is not null ? new("groupDateUnit", FieldIssue.NotAllowed)
            : null;
    }

    /// <summary>What is wrong with one column, or null; the field is relative to the column (<c>""</c>, <c>.kind</c>, <c>.key</c> or <c>.aggregation</c>).</summary>
    private static FieldIssue? ColumnIssue(PortfolioTableColumn? column, Dictionary<string, PortfolioFieldDescriptor> catalogue)
    {
        if (column?.Key is null)
        {
            return new(string.Empty, FieldIssue.Required);
        }

        IReadOnlyList<PortfolioTableAggregation>? allowed = column.Kind switch
        {
            PortfolioTableColumnKind.Dimension => ColumnDimensions.ContainsKey(column.Key)
                ? [PortfolioTableAggregation.None, PortfolioTableAggregation.Count, PortfolioTableAggregation.CountDistinct]
                : null,
            PortfolioTableColumnKind.Field => catalogue.GetValueOrDefault(column.Key) is { Grain: PortfolioFieldGrain.Project } field
                ? [PortfolioTableAggregation.None, PortfolioTableAggregation.CountDistinct, .. field.Aggregations.Select(TableAggregationOf)]
                : null,
            PortfolioTableColumnKind.Measure => ColumnMeasures.ContainsKey(column.Key) ? [] : null,
            _ => null,
        };
        return !Enum.IsDefined(column.Kind) ? new(".kind", FieldIssue.NotAllowed)
            : allowed is null ? new(".key", FieldIssue.NotAllowed)
            : column.Aggregation is not { } aggregation ? column.Kind == PortfolioTableColumnKind.Measure ? null : new(".aggregation", FieldIssue.Required)
            : !allowed.Contains(aggregation) ? new(".aggregation", FieldIssue.NotAllowed)
            : null;
    }

    private static PortfolioTableAggregation TableAggregationOf(PortfolioFieldAggregation aggregation) => aggregation switch
    {
        PortfolioFieldAggregation.Count => PortfolioTableAggregation.Count,
        PortfolioFieldAggregation.Sum => PortfolioTableAggregation.Sum,
        PortfolioFieldAggregation.Average => PortfolioTableAggregation.Average,
        PortfolioFieldAggregation.Min => PortfolioTableAggregation.Min,
        PortfolioFieldAggregation.Max => PortfolioTableAggregation.Max,
        PortfolioFieldAggregation.CountTrue => PortfolioTableAggregation.CountTrue,
        PortfolioFieldAggregation.ShareTrue => PortfolioTableAggregation.ShareTrue,
        _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown aggregation."),
    };

    private static PortfolioFieldAggregation FieldAggregationOf(PortfolioTableAggregation aggregation) => aggregation switch
    {
        PortfolioTableAggregation.Count => PortfolioFieldAggregation.Count,
        PortfolioTableAggregation.Sum => PortfolioFieldAggregation.Sum,
        PortfolioTableAggregation.Average => PortfolioFieldAggregation.Average,
        PortfolioTableAggregation.Min => PortfolioFieldAggregation.Min,
        PortfolioTableAggregation.Max => PortfolioFieldAggregation.Max,
        PortfolioTableAggregation.CountTrue => PortfolioFieldAggregation.CountTrue,
        PortfolioTableAggregation.ShareTrue => PortfolioFieldAggregation.ShareTrue,
        PortfolioTableAggregation.None or PortfolioTableAggregation.CountDistinct =>
            throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Not a field aggregation."),
        _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown aggregation."),
    };

    /// <summary>
    /// The names of the items, departments and entities the column tables group by: each item catalogue's published items, and the departments and entities
    /// of <paramref name="projects"/>; keyed by the id as <see cref="KeyOf"/> writes it.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, BilingualLabel>> TableLabelsAsync(
        IEnumerable<PortfolioVisualInput> drawn, IEnumerable<PortfolioProjectRow> projects, CancellationToken cancellationToken)
    {
        HashSet<PortfolioGroupBy> named = [.. drawn.Where(IsColumnTable).SelectMany(v => v.Columns!)
            .Where(c => c is { Kind: PortfolioTableColumnKind.Dimension, Aggregation: PortfolioTableAggregation.None })
            .Select(c => ColumnDimensions.GetValueOrDefault(c.Key)).OfType<PortfolioGroupBy>()];
        Dictionary<string, BilingualLabel> labels = new(StringComparer.Ordinal);
        foreach (string catalogueCode in named.Select(ItemCatalogueOf).OfType<string>())
        {
            foreach (MasterDataItemReference item in await masterData.ListPublishedItemsAsync(catalogueCode, cancellationToken).ConfigureAwait(false))
            {
                labels[item.Id.ToString()] = item.Label;
            }
        }

        if (named.Contains(PortfolioGroupBy.Department) || named.Contains(PortfolioGroupBy.Entity))
        {
            List<Domain.Project.Project> reached = [.. projects.Select(r => r.Project)];
            OrganizationNames names = await organizations.ListNamesAsync(
                [.. reached.Select(p => p.DepartmentId).Distinct()], [.. reached.Select(p => p.ExternalEntityId).OfType<Guid>().Distinct()], cancellationToken).ConfigureAwait(false);
            foreach ((Guid id, BilingualLabel label) in names.Departments.Concat(names.ExternalEntities))
            {
                labels[id.ToString()] = label;
            }
        }

        return labels;
    }

    private static string? ItemCatalogueOf(PortfolioGroupBy groupBy) => groupBy switch
    {
        PortfolioGroupBy.Portfolio => MasterDataCatalogueCodes.StrategicPortfolio,
        PortfolioGroupBy.Initiative => MasterDataCatalogueCodes.StrategicInitiative,
        PortfolioGroupBy.Sector => MasterDataCatalogueCodes.ProjectClassification,
        PortfolioGroupBy.Region => MasterDataCatalogueCodes.Region,
        PortfolioGroupBy.City => MasterDataCatalogueCodes.City,
        PortfolioGroupBy.Priority => MasterDataCatalogueCodes.Priority,
        PortfolioGroupBy.None or PortfolioGroupBy.Department or PortfolioGroupBy.Entity or PortfolioGroupBy.Status or PortfolioGroupBy.Stage
            or PortfolioGroupBy.OnPlan => null,
        _ => null,
    };

    /// <summary>
    /// A column table's figures (dashboard-designer-power-table.md): its projects narrowed by its filters, grouped by the tuple of its <c>NONE</c> columns' values
    /// (one row per project by the title or ID; one row when none), each other column calculated over the row's projects — a measure exactly as the insights'
    /// breakdown row (<see cref="PortfolioInsightsService.RowOf"/>), a field over its revealed cells (<see cref="Aggregate"/>). Rows run by their first grouped
    /// column, then the next: a scale (status, stage, on-plan) in its order, a named cell by its Arabic name, a number or date by its value, a code by itself,
    /// missing last; at most <see cref="MaxTableRows"/>. The totals are calculated over every row's projects.
    /// </summary>
    internal static PortfolioTableResult Table(
        PortfolioVisualInput visual, IReadOnlyList<PortfolioProjectRow> rows, ProjectStageSet? stages, bool amounts, OnPlanThresholds thresholds,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells, IReadOnlyDictionary<string, PortfolioFieldDescriptor> catalogue,
        IReadOnlyDictionary<string, BilingualLabel> labels)
    {
        IReadOnlyList<PortfolioTableColumn> columns = visual.Columns ?? [];
        List<FieldRow> projects = FilteredProjects(visual, rows, cells, catalogue);
        List<int> grouped = [.. Enumerable.Range(0, columns.Count).Where(i => columns[i].Aggregation == PortfolioTableAggregation.None)];
        bool perProject = grouped.Any(i => columns[i] is { Kind: PortfolioTableColumnKind.Dimension, Key: TitleKey or FormalProjectIdKey });

        // Each project's grouped cells, with the value its row is grouped and sorted by.
        List<(FieldRow Member, GroupedCell[] Keys)> keyed = [.. projects.Select(m => (m, grouped.Select(i => GroupedCellOf(columns[i], m)).ToArray()))];
        List<(GroupedCell[] Keys, List<FieldRow> Members)> groups = grouped.Count == 0
            ? [([], projects)]
            : [.. keyed.GroupBy(k => string.Join('\u001F', k.Keys.Select(c => c.GroupKey ?? "\0")), StringComparer.Ordinal)
                .Select(g => (g.First().Keys, g.Select(k => k.Member).ToList()))
                .OrderBy(g => g.Keys, GroupedCellComparer.Instance)];

        List<PortfolioTableRow> tableRows = [.. groups.Take(MaxTableRows).Select(g =>
            new PortfolioTableRow(g.Members.Count, perProject ? g.Members[0].Project.Project.Id : null, CellsOf(g.Members, g.Keys)))];
        return new PortfolioTableResult(
            [.. columns.Select(c => ColumnTypeOf(c, catalogue))], tableRows,
            visual.ShowTotals == false ? null : CellsOf(projects, null),
            groups.Count > MaxTableRows);

        IReadOnlyList<PortfolioTableCell> CellsOf(List<FieldRow> members, GroupedCell[]? keys)
        {
            List<PortfolioProjectRow> memberRows = [.. members.Select(m => m.Project)];
            PortfolioBreakdownRow? figures = null;
            PortfolioBreakdownRow Figures() => figures ??= PortfolioInsightsService.RowOf(null, null, memberRows, amounts, thresholds, sector: false);
            List<PortfolioTableCell> result = [];
            for (int i = 0; i < columns.Count; i++)
            {
                int place = grouped.IndexOf(i);
                result.Add(place >= 0
                    ? keys?[place].Cell ?? new PortfolioTableCell(null, null, null)
                    : CalculatedCell(columns[i], members, memberRows, Figures, projects.Count));
            }

            return result;
        }

        GroupedCell GroupedCellOf(PortfolioTableColumn column, FieldRow member)
        {
            Domain.Project.Project p = member.Project.Project;
            if (column.Kind == PortfolioTableColumnKind.Field)
            {
                PortfolioFieldDescriptor? field = catalogue.GetValueOrDefault(column.Key);
                string? key = TableKeyOf(field, member.Cell(column.Key));
                decimal? number = key is null || field is null ? null : field.IsDate() ? DayNumber(key) : Number(key);
                bool masked = key is null && field is { IsSensitive: true } && !amounts;
                return new GroupedCell(key, new PortfolioTableCell(key, null, number, masked), number, field?.Type is PortfolioFieldType.Code or PortfolioFieldType.Boolean ? key : null, null);
            }

            if (column.Key == TitleKey)
            {
                return new GroupedCell(p.Id.ToString(), new PortfolioTableCell(p.Id.ToString(), new BilingualLabel(p.Title.Text, p.Title.Text), null), null, p.Title.Text, null);
            }

            if (column.Key == FormalProjectIdKey)
            {
                return new GroupedCell(p.Id.ToString(), new PortfolioTableCell(p.FormalProjectId, null, null), null, p.FormalProjectId, null);
            }

            PortfolioGroupBy dimension = ColumnDimensions[column.Key]!.Value;
            string? code = KeyOf(dimension, member.Project);
            BilingualLabel? label = code is null ? null : dimension == PortfolioGroupBy.Stage ? stages?.Find(code)?.Name : labels.GetValueOrDefault(code);
            int? scale = code is not null && dimension is PortfolioGroupBy.Status or PortfolioGroupBy.Stage or PortfolioGroupBy.OnPlan
                ? ScaleOrder(dimension, member.Project, stages)
                : null;
            return new GroupedCell(code, new PortfolioTableCell(code, label, null), null, label?.Ar ?? code, scale);
        }

        PortfolioTableCell CalculatedCell(
            PortfolioTableColumn column, List<FieldRow> members, List<PortfolioProjectRow> memberRows, Func<PortfolioBreakdownRow> figures, int allProjects)
        {
            switch (column.Kind)
            {
                case PortfolioTableColumnKind.Measure:
                    return MeasureCell(ColumnMeasures[column.Key], figures(), memberRows, amounts, allProjects);
                case PortfolioTableColumnKind.Dimension:
                    List<string> keys = [.. members.Select(m => DimensionKeyOf(column.Key, m.Project)).OfType<string>()];
                    return Counted(column.Aggregation == PortfolioTableAggregation.CountDistinct ? keys.Distinct(StringComparer.Ordinal).Count() : keys.Count);
                case PortfolioTableColumnKind.Field:
                    PortfolioFieldDescriptor? field = catalogue.GetValueOrDefault(column.Key);
                    if (field is { IsSensitive: true } && !amounts)
                    {
                        return new PortfolioTableCell(null, null, null, Masked: true);
                    }

                    List<string?> values = [.. members.Select(m => m.Cell(column.Key))];
                    return column.Aggregation == PortfolioTableAggregation.CountDistinct
                        ? Counted(values.Select(v => TableKeyOf(field, v)).OfType<string>().Distinct(StringComparer.Ordinal).Count())
                        : new PortfolioTableCell(null, null, Aggregate(FieldAggregationOf(column.Aggregation!.Value), field?.Type ?? PortfolioFieldType.Count, values));
                default:
                    throw new ArgumentOutOfRangeException(nameof(column), column.Kind, "Unknown column kind.");
            }
        }
    }

    private static PortfolioTableCell Counted(int count) => new(null, null, count);

    /// <summary>A measure over a row's projects: the insights' figures of the group (<paramref name="figures"/>), an amount masked without the amounts permission.</summary>
    private static PortfolioTableCell MeasureCell(TableMeasure measure, PortfolioBreakdownRow figures, IReadOnlyList<PortfolioProjectRow> members, bool amounts, int allProjects) => measure switch
    {
        TableMeasure.ValueSar => amounts ? new(null, null, ValueOf(PortfolioMeasure.ValueSar, figures, members)) : new(null, null, null, Masked: true),
        TableMeasure.Measured => new(null, null, figures.Measured),
        TableMeasure.OnPlan => new(figures.OnPlan is { } onPlan ? Code(onPlan) : null, null, null),
        TableMeasure.ProjectShare => new(null, null, allProjects == 0 ? null : decimal.Round(figures.Projects * 100m / allProjects, 1)),
        TableMeasure.Projects => new(null, null, ValueOf(PortfolioMeasure.Projects, figures, members)),
        TableMeasure.InProgress => new(null, null, ValueOf(PortfolioMeasure.InProgress, figures, members)),
        TableMeasure.AveragePlanned => new(null, null, ValueOf(PortfolioMeasure.AveragePlanned, figures, members)),
        TableMeasure.AverageActual => new(null, null, ValueOf(PortfolioMeasure.AverageActual, figures, members)),
        TableMeasure.GapPoints => new(null, null, ValueOf(PortfolioMeasure.GapPoints, figures, members)),
        TableMeasure.NeedingFollowUp => new(null, null, ValueOf(PortfolioMeasure.NeedingFollowUp, figures, members)),
        TableMeasure.OverdueActions or TableMeasure.OnTrack or TableMeasure.Late or TableMeasure.Stalled or TableMeasure.NotMeasured or TableMeasure.FollowUpStalled
            or TableMeasure.FollowUpSuspended or TableMeasure.FollowUpLate or TableMeasure.FollowUpActionOverdue or TableMeasure.FollowUpOpenObstacle =>
            new(null, null, ValueOf(FigureOf(measure)!.Value, figures, members)),
        _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, "Unknown measure."),
    };

    /// <summary>The portfolio figure a table measure is, by the same code; null for the table's own (MEASURED, ON_PLAN, PROJECT_SHARE).</summary>
    private static PortfolioMeasure? FigureOf(TableMeasure measure) =>
        Enum.TryParse(measure.ToString(), out PortfolioMeasure figure) ? figure : null;

    /// <summary>A project's value of a dimension column for counting: the attribute's key, the project for its title, its formal ID.</summary>
    private static string? DimensionKeyOf(string key, PortfolioProjectRow row) => key switch
    {
        TitleKey => row.Project.Id.ToString(),
        FormalProjectIdKey => row.Project.FormalProjectId,
        _ => KeyOf(ColumnDimensions[key]!.Value, row),
    };

    /// <summary>A field's cell as a table groups and counts it: a yes/no as <c>TRUE</c> or <c>FALSE</c>, a date or moment as its (UTC) date, anything else as written.</summary>
    internal static string? TableKeyOf(PortfolioFieldDescriptor? field, string? value) =>
        value is null || field is null ? value
        : field.Type == PortfolioFieldType.Boolean ? value.ToUpperInvariant()
        : field.IsDate() ? (value.Length >= 10 ? value[..10] : null)
        : value;

    private static PortfolioTableColumnType ColumnTypeOf(PortfolioTableColumn column, IReadOnlyDictionary<string, PortfolioFieldDescriptor> catalogue) =>
        column.Aggregation is PortfolioTableAggregation.Count or PortfolioTableAggregation.CountDistinct or PortfolioTableAggregation.CountTrue ? PortfolioTableColumnType.Count
        : column.Aggregation == PortfolioTableAggregation.ShareTrue ? PortfolioTableColumnType.Percent
        : column.Kind switch
        {
            PortfolioTableColumnKind.Dimension => column.Key switch
            {
                TitleKey => PortfolioTableColumnType.Text,
                FormalProjectIdKey => PortfolioTableColumnType.Code,
                _ => (ColumnDimensions[column.Key] ?? PortfolioGroupBy.None) switch
                {
                    PortfolioGroupBy.Status or PortfolioGroupBy.Stage or PortfolioGroupBy.None => PortfolioTableColumnType.Code,
                    PortfolioGroupBy.OnPlan => PortfolioTableColumnType.OnPlan,
                    PortfolioGroupBy.Portfolio or PortfolioGroupBy.Initiative or PortfolioGroupBy.Sector or PortfolioGroupBy.Department or PortfolioGroupBy.Entity
                        or PortfolioGroupBy.Region or PortfolioGroupBy.City or PortfolioGroupBy.Priority => PortfolioTableColumnType.Item,
                    _ => PortfolioTableColumnType.Item,
                },
            },
            PortfolioTableColumnKind.Field => (catalogue.GetValueOrDefault(column.Key)?.Type ?? PortfolioFieldType.Count) switch
            {
                PortfolioFieldType.Code => PortfolioTableColumnType.Code,
                PortfolioFieldType.Count => PortfolioTableColumnType.Count,
                PortfolioFieldType.Percent => PortfolioTableColumnType.Percent,
                PortfolioFieldType.Days => PortfolioTableColumnType.Days,
                PortfolioFieldType.Sar => PortfolioTableColumnType.Sar,
                PortfolioFieldType.Boolean => PortfolioTableColumnType.Boolean,
                PortfolioFieldType.Date or PortfolioFieldType.DateTime => PortfolioTableColumnType.Date,
                _ => PortfolioTableColumnType.Count,
            },
            PortfolioTableColumnKind.Measure => ColumnMeasures[column.Key] switch
            {
                TableMeasure.ValueSar => PortfolioTableColumnType.Sar,
                TableMeasure.AveragePlanned or TableMeasure.AverageActual or TableMeasure.ProjectShare => PortfolioTableColumnType.Percent,
                TableMeasure.GapPoints => PortfolioTableColumnType.Points,
                TableMeasure.OnPlan => PortfolioTableColumnType.OnPlan,
                TableMeasure.Projects or TableMeasure.InProgress or TableMeasure.NeedingFollowUp or TableMeasure.OverdueActions or TableMeasure.Measured
                    or TableMeasure.OnTrack or TableMeasure.Late or TableMeasure.Stalled or TableMeasure.NotMeasured or TableMeasure.FollowUpStalled
                    or TableMeasure.FollowUpSuspended or TableMeasure.FollowUpLate or TableMeasure.FollowUpActionOverdue or TableMeasure.FollowUpOpenObstacle =>
                    PortfolioTableColumnType.Count,
                _ => PortfolioTableColumnType.Count,
            },
            _ => PortfolioTableColumnType.Text,
        };

    /// <summary>The figures a table column takes: the fixed measures, then the projects measured, the on-plan status and the share of the table's projects, then the box sections' figures.</summary>
    private enum TableMeasure
    {
        Projects = 1,
        InProgress = 2,
        ValueSar = 3,
        AveragePlanned = 4,
        AverageActual = 5,
        GapPoints = 6,
        NeedingFollowUp = 7,
        OverdueActions = 8,
        Measured = 9,
        OnPlan = 10,
        ProjectShare = 11,

        // The 20 Nov 2026 box sections: the on-plan indicator's and the follow-up reasons' boxes, as the portfolio figures of the same name.
        OnTrack = 12,
        Late = 13,
        Stalled = 14,
        NotMeasured = 15,
        FollowUpStalled = 16,
        FollowUpSuspended = 17,
        FollowUpLate = 18,
        FollowUpActionOverdue = 19,
        FollowUpOpenObstacle = 20,
    }

    /// <summary>
    /// A grouped column's cell of a project: <see cref="GroupKey"/> what rows are grouped by (the project itself for its title or ID), the cell drawn, and what
    /// the rows are sorted by — a place on a scale, a number, or a text.
    /// </summary>
    private sealed record GroupedCell(string? GroupKey, PortfolioTableCell Cell, decimal? Number, string? Text, int? Scale)
    {
        public bool IsMissing => Scale is null && Number is null && Text is null;
    }

    /// <summary>Orders rows by their first grouped cell, then the next: missing last, then a scale, a number, a text (ordinal, ignoring case).</summary>
    private sealed class GroupedCellComparer : IComparer<GroupedCell[]>
    {
        public static GroupedCellComparer Instance { get; } = new();

        public int Compare(GroupedCell[]? x, GroupedCell[]? y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);
            for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
            {
                int order = x[i].IsMissing.CompareTo(y[i].IsMissing);
                order = order != 0 ? order : Nullable.Compare(x[i].Scale, y[i].Scale);
                order = order != 0 ? order : Nullable.Compare(x[i].Number, y[i].Number);
                order = order != 0 ? order : string.Compare(x[i].Text, y[i].Text, StringComparison.OrdinalIgnoreCase);
                if (order != 0)
                {
                    return order;
                }
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
