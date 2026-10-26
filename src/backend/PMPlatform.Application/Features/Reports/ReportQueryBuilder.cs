using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>A column the plan reads: shown in the output, or read only so a filter or a sort can use it.</summary>
internal sealed record PlannedColumn(ReportField Field, BilingualLabel Label, Guid? ClassificationId, bool IsShown, bool IsDefaultVisible);

/// <summary>A filter on a planned column, its values already parsed to the column's type.</summary>
internal sealed record PlannedFilter(int Column, ReportFilterOperator Operator, IReadOnlyList<string> Values);

internal sealed record PlannedSort(int Column, ReportSortDirection Direction);

/// <summary>
/// A validated report request: the projection whose permission decides the rows, the fields it reads — every one a field of FG-01's register or of
/// the project's identity, named by a published report version or the REPORT_RULES allowlist — the filters and sorts on them, and the project or
/// department it is narrowed to. Nothing in it is text a caller wrote except parsed filter values.
/// </summary>
internal sealed record ReportPlan(
    ProjectionDescriptor Basis, IReadOnlyList<PlannedColumn> Columns, IReadOnlyList<PlannedFilter> Filters, IReadOnlyList<PlannedSort> Sort, Guid? ProjectId, Guid? DepartmentId)
{
    public IEnumerable<int> ShownColumns => Columns.Select((c, i) => (c, i)).Where(x => x.c.IsShown).Select(x => x.i);
}

/// <summary>
/// The allowlisted query builder (TASK-071 deliverable; FG-02 §6, §11.3). It turns a request into a <see cref="ReportPlan"/> or refuses it, field
/// by field, before anything is read: a published report takes only its own columns and parameters; the SCR-138 explorer takes only the fields
/// the REPORT_RULES allowlist in force names — a field of a projection it names, a projection it names (the join to the project row), filters
/// only where the entry is filterable and with the operators the field's type takes, sorts only where it is sortable. There is no other way to
/// name a field, a join, an expression or a query (BR-RPT-045, BR-RPT-046).
/// </summary>
internal static class ReportQueryBuilder
{
    public const int MaxExplorerColumns = 40;

    public const int MaxFilters = 20;

    /// <summary>A run of a published report version: its parameters, a choice among its columns, an order among them.</summary>
    public static AdministrationResult<ReportPlan> ForReport(
        ReportFields fields,
        ReportCode code,
        string primaryProjectionCode,
        IReadOnlyList<ReportColumn> columns,
        IReadOnlyList<(ReportParameter Parameter, IReadOnlyList<ReportParameterOption> Options)> parameters,
        ReportRunInput input)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(input);
        ProjectionDescriptor basis = fields.Projection(primaryProjectionCode)
                                     ?? throw new InvalidOperationException($"{code}: its primary projection {primaryProjectionCode} is not registered.");
        List<(ReportColumn Column, ReportField Field)> published = [.. columns.OrderBy(c => c.SortOrder).Select(c =>
            (c, fields.Find(c.SourceEntityCode, c.FieldCode) ?? throw new InvalidOperationException($"{code}: column {c.SourceEntityCode}.{c.FieldCode} is not registered.")))];

        // Parameters: each named once, each of its type and among its options; the required ones present (FG-02 §6.1).
        List<FieldIssue> parameterIssues = [];
        Dictionary<string, string> given = new(StringComparer.Ordinal);
        for (int i = 0; i < input.Parameters.Count; i++)
        {
            ReportParameterInput p = input.Parameters[i];
            if (!parameters.Any(x => x.Parameter.Code == p.Code))
            {
                parameterIssues.Add(new FieldIssue($"parameters[{i}].code", FieldIssue.NotFound));
            }
            else if (!given.TryAdd(p.Code, p.Value))
            {
                parameterIssues.Add(new FieldIssue($"parameters[{i}].code", FieldIssue.Duplicate));
            }
        }

        Guid? projectId = null;
        Guid? departmentId = null;
        List<(ReportField Field, string Value)> optionFilters = [];
        foreach ((ReportParameter parameter, IReadOnlyList<ReportParameterOption> options) in parameters)
        {
            int index = input.Parameters.ToList().FindIndex(p => p.Code == parameter.Code);
            if (!given.TryGetValue(parameter.Code, out string? value))
            {
                continue;
            }

            string at = $"parameters[{index}].value";
            switch (parameter.DataType)
            {
                case ReportParameterDataType.Project when Guid.TryParse(value, out Guid project):
                    projectId = project;
                    break;
                case ReportParameterDataType.Department when Guid.TryParse(value, out Guid department):
                    departmentId = department;
                    break;
                case ReportParameterDataType.Option when options.Any(o => o.ValueCode == value):
                    optionFilters.Add((fields.Find(parameter.SourceEntityCode!, parameter.FieldCode!)
                                       ?? throw new InvalidOperationException($"{code}: parameter {parameter.Code} is bound to no registered field."), value));
                    break;
                case ReportParameterDataType.Project or ReportParameterDataType.Department or ReportParameterDataType.Option:
                    parameterIssues.Add(new FieldIssue(at, ReportIssueCodes.ValueInvalid));
                    break;
                default:
                    throw new InvalidOperationException($"{code}: parameter {parameter.Code} has an unknown type.");
            }
        }

        if (parameterIssues.Count > 0)
        {
            return AdministrationError.Rule(ReportErrorCodes.ParameterInvalid, [.. parameterIssues]);
        }

        if (parameters.Where(x => x.Parameter.IsRequired && !given.ContainsKey(x.Parameter.Code)).Select(x => new FieldIssue($"parameters.{x.Parameter.Code}", FieldIssue.Required)).ToList()
            is { Count: > 0 } missing)
        {
            return AdministrationError.Rule(ReportErrorCodes.ParameterRequired, [.. missing]);
        }

        // Columns: a choice among the version's own; none given is its default columns (FG-02 §6.2).
        List<FieldIssue> columnIssues = [];
        List<string> chosen = [];
        if (input.Columns is null)
        {
            chosen = [.. published.Where(p => p.Column.IsDefaultVisible).Select(p => p.Field.Key)];
        }
        else if (input.Columns.Count == 0)
        {
            columnIssues.Add(new FieldIssue("columns", FieldIssue.Required));
        }
        else
        {
            for (int i = 0; i < input.Columns.Count; i++)
            {
                ReportFieldReference reference = input.Columns[i];
                string key = $"{reference.SourceEntityCode}.{reference.FieldCode}";
                if (!published.Any(p => p.Field.Key == key))
                {
                    columnIssues.Add(new FieldIssue($"columns[{i}]", ReportIssueCodes.FieldNotInReport));
                }
                else if (chosen.Contains(key))
                {
                    columnIssues.Add(new FieldIssue($"columns[{i}]", FieldIssue.Duplicate));
                }
                else
                {
                    chosen.Add(key);
                }
            }
        }

        if (columnIssues.Count > 0)
        {
            return AdministrationError.Rule(ReportErrorCodes.ColumnNotSupported, [.. columnIssues]);
        }

        List<FieldIssue> sortIssues = [];
        for (int i = 0; i < input.Sort.Count; i++)
        {
            ReportSortInput sort = input.Sort[i];
            if (published.FirstOrDefault(p => p.Field.Key == $"{sort.SourceEntityCode}.{sort.FieldCode}") is not { Field: { } field } || !field.IsSortableType
                || input.Sort.Take(i).Any(s => s.SourceEntityCode == sort.SourceEntityCode && s.FieldCode == sort.FieldCode))
            {
                sortIssues.Add(new FieldIssue($"sort[{i}]", FieldIssue.NotAllowed));
            }
        }

        if (sortIssues.Count > 0)
        {
            return AdministrationError.Rule(ReportErrorCodes.SortNotSupported, [.. sortIssues]);
        }

        // Shown columns in the caller's order; then the version's other columns a parameter or a sort needs, read and not shown.
        List<PlannedColumn> planned = [.. chosen.Select(key => published.Single(p => p.Field.Key == key)).Select(p => Planned(p.Column, p.Field, isShown: true))];
        foreach (string key in optionFilters.Select(f => f.Field.Key).Concat(input.Sort.Select(s => $"{s.SourceEntityCode}.{s.FieldCode}")))
        {
            if (!planned.Any(c => c.Field.Key == key) && published.FirstOrDefault(p => p.Field.Key == key) is { Column: not null } hidden)
            {
                planned.Add(Planned(hidden.Column, hidden.Field, isShown: false));
            }
        }

        // ADM-037's validation binds an OPTION parameter to one of the version's own columns only.
        return optionFilters.Any(f => !planned.Any(c => c.Field.Key == f.Field.Key))
            ? throw new InvalidOperationException($"{code}: a parameter filters a field the version does not publish.")
            : new ReportPlan(
            basis,
            planned,
            [.. optionFilters.Select(f => new PlannedFilter(planned.FindIndex(c => c.Field.Key == f.Field.Key), ReportFilterOperator.Eq, [f.Value]))],
            [.. input.Sort.Select(s => new PlannedSort(planned.FindIndex(c => c.Field.Key == $"{s.SourceEntityCode}.{s.FieldCode}"), s.Direction))],
            projectId,
            departmentId);
    }

    /// <summary>
    /// An SCR-138 composition. Every column, filter and sort must name an entry of <paramref name="allowlist"/>, the REPORT_RULES version in force;
    /// anything else is refused, column by column (TASK-071 acceptance criterion 1).
    /// </summary>
    public static AdministrationResult<ReportPlan> ForExplorer(ReportFields fields, IReadOnlyList<ReportAllowlistEntryReference> allowlist, ExplorerRunInput input)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(input);
        ProjectionDescriptor basis = fields.Projection(ReportFields.ExplorerBasis) ?? throw new InvalidOperationException("The explorer's basis is not registered.");
        HashSet<string> joins = new(allowlist.Select(a => a.SourceEntityCode), StringComparer.Ordinal);

        List<FieldIssue> columnIssues = [];
        List<PlannedColumn> planned = [];
        if (input.Columns.Count == 0)
        {
            columnIssues.Add(new FieldIssue("columns", FieldIssue.Required));
        }
        else if (input.Columns.Count > MaxExplorerColumns)
        {
            columnIssues.Add(new FieldIssue("columns", FieldIssue.NotAllowed));
        }

        for (int i = 0; i < input.Columns.Count && input.Columns.Count <= MaxExplorerColumns; i++)
        {
            ReportFieldReference reference = input.Columns[i];
            if (Resolve(fields, allowlist, joins, basis, reference.SourceEntityCode, reference.FieldCode, out ReportAllowlistEntryReference? entry, out ReportField? field) is { } refusal)
            {
                columnIssues.Add(new FieldIssue($"columns[{i}]", refusal));
            }
            else if (planned.Any(c => c.Field.Key == field!.Key))
            {
                columnIssues.Add(new FieldIssue($"columns[{i}]", FieldIssue.Duplicate));
            }
            else
            {
                planned.Add(new PlannedColumn(field!, entry!.Label, entry.DataClassificationItemId, IsShown: true, IsDefaultVisible: true));
            }
        }

        if (columnIssues.Count > 0)
        {
            return AdministrationError.Rule(ReportErrorCodes.ColumnNotSupported, [.. columnIssues]);
        }

        // Filters: on allowlisted, filterable fields, with an operator of the field's type and values of the field (FG-02 §6.1).
        List<FieldIssue> filterIssues = [];
        List<FieldIssue> valueIssues = [];
        List<PlannedFilter> filters = [];
        if (input.Filters.Count > MaxFilters)
        {
            filterIssues.Add(new FieldIssue("filters", FieldIssue.NotAllowed));
        }

        for (int i = 0; i < input.Filters.Count && input.Filters.Count <= MaxFilters; i++)
        {
            ReportFilterInput filter = input.Filters[i];
            if (Resolve(fields, allowlist, joins, basis, filter.SourceEntityCode, filter.FieldCode, out ReportAllowlistEntryReference? entry, out ReportField? field) is { } refusal)
            {
                filterIssues.Add(new FieldIssue($"filters[{i}]", refusal));
            }
            else if (!entry!.IsFilterable)
            {
                filterIssues.Add(new FieldIssue($"filters[{i}]", FieldIssue.NotAllowed));
            }
            else if (!field!.Operators.Contains(filter.Operator))
            {
                filterIssues.Add(new FieldIssue($"filters[{i}].operator", ReportIssueCodes.OperatorNotSupported));
            }
            else if (Values(field, filter.Operator, filter.Value) is not { } values)
            {
                valueIssues.Add(new FieldIssue($"filters[{i}].value", ReportIssueCodes.ValueInvalid));
            }
            else
            {
                filters.Add(new PlannedFilter(ColumnOf(planned, field, entry), filter.Operator, values));
            }
        }

        if (filterIssues.Count > 0)
        {
            return AdministrationError.Rule(ReportErrorCodes.FilterNotSupported, [.. filterIssues]);
        }

        if (valueIssues.Count > 0)
        {
            return AdministrationError.Rule(ReportErrorCodes.FilterValueInvalid, [.. valueIssues]);
        }

        List<FieldIssue> sortIssues = [];
        List<PlannedSort> sorts = [];
        for (int i = 0; i < input.Sort.Count; i++)
        {
            ReportSortInput sort = input.Sort[i];
            if (Resolve(fields, allowlist, joins, basis, sort.SourceEntityCode, sort.FieldCode, out ReportAllowlistEntryReference? entry, out ReportField? field) is { } refusal)
            {
                sortIssues.Add(new FieldIssue($"sort[{i}]", refusal));
            }
            else if (!entry!.IsSortable || !field!.IsSortableType || input.Sort.Take(i).Any(s => s.SourceEntityCode == sort.SourceEntityCode && s.FieldCode == sort.FieldCode))
            {
                sortIssues.Add(new FieldIssue($"sort[{i}]", FieldIssue.NotAllowed));
            }
            else
            {
                sorts.Add(new PlannedSort(ColumnOf(planned, field, entry), sort.Direction));
            }
        }

        return sortIssues.Count > 0
            ? AdministrationError.Rule(ReportErrorCodes.SortNotSupported, [.. sortIssues])
            : new ReportPlan(basis, planned, filters, sorts, null, null);
    }

    /// <summary>The values of a filter for its operator, each a value of the field; null if any is not.</summary>
    public static IReadOnlyList<string>? Values(ReportField field, ReportFilterOperator op, string text)
    {
        if (ReportValues.Split(op, text) is not { } parts)
        {
            return null;
        }

        List<string> values = [];
        foreach (string part in parts)
        {
            if (!ReportValues.TryParse(field, part, out string value))
            {
                return null;
            }

            values.Add(value);
        }

        return values;
    }

    /// <summary>
    /// The allowlist entry and register field a reference names, or why there is none: a projection the allowlist does not name (a join it does
    /// not allow), a field of it the allowlist does not name, a field the register does not have, or one of another grain.
    /// </summary>
    private static string? Resolve(
        ReportFields fields, IReadOnlyList<ReportAllowlistEntryReference> allowlist, HashSet<string> joins, ProjectionDescriptor basis, string entityCode, string fieldCode,
        out ReportAllowlistEntryReference? entry, out ReportField? field)
    {
        entry = allowlist.FirstOrDefault(a => a.SourceEntityCode == entityCode && a.FieldCode == fieldCode);
        field = entry is null ? null : fields.Find(entityCode, fieldCode);
        return entry is null ? joins.Contains(entityCode) ? ReportIssueCodes.FieldNotAllowlisted : ReportIssueCodes.JoinNotAllowlisted
            : field is null ? ReportIssueCodes.ProjectionNotRegistered
            : !field.FitsGrain(basis) ? ReportIssueCodes.GrainMismatch
            : null;
    }

    /// <summary>The column a filter or sort reads: a shown column of the field, or a new column read and not shown.</summary>
    private static int ColumnOf(List<PlannedColumn> planned, ReportField field, ReportAllowlistEntryReference entry)
    {
        int index = planned.FindIndex(c => c.Field.Key == field.Key);
        if (index >= 0)
        {
            return index;
        }

        planned.Add(new PlannedColumn(field, entry.Label, entry.DataClassificationItemId, IsShown: false, IsDefaultVisible: false));
        return planned.Count - 1;
    }

    private static PlannedColumn Planned(ReportColumn column, ReportField field, bool isShown) =>
        new(field, column.Label, column.DataClassificationItemId, isShown, column.IsDefaultVisible);
}
