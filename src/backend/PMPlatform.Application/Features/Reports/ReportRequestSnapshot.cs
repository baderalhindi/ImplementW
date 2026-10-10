using System.Text.Json;
using System.Text.Json.Serialization;
using PMPlatform.Application.Features.Reports.Contracts;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// A job's request as it was accepted (FG-02 REP-006 to -008: parameter, column and sort/group snapshots): the parameters, the columns shown —
/// always explicit, never "the defaults", so a later version's defaults cannot change what was asked — the filters and the sorts. The job runs
/// this and nothing else; its report version is pinned on the job.
/// </summary>
internal sealed record ReportRequestSnapshot(
    IReadOnlyList<ReportParameterInput> Parameters,
    IReadOnlyList<ReportFieldReference> Columns,
    IReadOnlyList<ReportFilterInput> Filters,
    IReadOnlyList<ReportSortInput> Sort)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    public static ReportRequestSnapshot Of(ReportPlan plan, IReadOnlyList<ReportParameterInput> parameters)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(
            parameters,
            [.. plan.ShownColumns.Select(i => new ReportFieldReference(plan.Columns[i].Field.SourceEntityCode, plan.Columns[i].Field.FieldCode))],
            [.. plan.Filters.Select(f => new ReportFilterInput(plan.Columns[f.Column].Field.SourceEntityCode, plan.Columns[f.Column].Field.FieldCode, f.Operator, string.Join(',', f.Values)))],
            [.. plan.Sort.Select(s => new ReportSortInput(plan.Columns[s.Column].Field.SourceEntityCode, plan.Columns[s.Column].Field.FieldCode, s.Direction))]);
    }

    public static ReportRequestSnapshot Parse(string json) =>
        JsonSerializer.Deserialize<ReportRequestSnapshot>(json, Options) ?? throw new InvalidOperationException("A job's request snapshot is empty.");

    /// <summary>The report run it is: an explorer composition's filters are the report's own parameters there.</summary>
    public ReportRunInput ToReportInput() => new(Parameters, Columns, Sort);

    public ExplorerRunInput ToExplorerInput() => new(Columns, Filters, Sort);

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Whether two requests ask for the same thing: what a repeated key must match (R-37).</summary>
    public bool SameAs(ReportRequestSnapshot other) => string.Equals(ToJson(), other?.ToJson(), StringComparison.Ordinal);
}
