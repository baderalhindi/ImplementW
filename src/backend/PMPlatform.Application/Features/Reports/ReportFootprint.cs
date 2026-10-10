using System.Text.Json;

namespace PMPlatform.Application.Features.Reports;

/// <summary>One project of an output, and the columns whose value the output shows for it.</summary>
internal sealed record ReportFootprintRow(Guid ProjectId, IReadOnlyList<int> Revealed);

/// <summary>
/// What an output holds, for authorising its download (TASK-071 acceptance criterion 2): the columns read — shown, or read for a filter or a
/// sort — and each project with the columns it revealed. A download runs the job's request again as its requester now, through the same
/// authorization as the data, and is allowed only if every project is still among the rows the requester may see and no value revealed then is
/// RESTRICTED or masked for them now. Authorization is compared, never data: a value since changed, gone missing or no longer applicable denies
/// nothing (BR-RPT-030).
/// </summary>
internal sealed record ReportFootprint(IReadOnlyList<string> Columns, IReadOnlyList<ReportFootprintRow> Rows)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<Guid> ProjectIds => [.. Rows.Select(r => r.ProjectId)];

    public static ReportFootprint Of(ReportExecution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);
        return new(
            [.. execution.Plan.Columns.Select(c => c.Field.Key)],
            [.. execution.Rows.GroupBy(r => r.ProjectId).Select(g => new ReportFootprintRow(g.Key, Revealed(g)))]);
    }

    public static ReportFootprint Parse(string json) =>
        JsonSerializer.Deserialize<ReportFootprint>(json, Options) ?? throw new InvalidOperationException("An output's footprint is empty.");

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Whether <paramref name="now"/> — the same request run for the requester now — still shows everything this footprint revealed.</summary>
    public bool IsCoveredBy(ReportExecution now)
    {
        ArgumentNullException.ThrowIfNull(now);
        List<string> columns = [.. now.Plan.Columns.Select(c => c.Field.Key)];
        Dictionary<Guid, HashSet<int>> withheld = now.Rows.GroupBy(r => r.ProjectId).ToDictionary(g => g.Key, g => new HashSet<int>(Withheld(g)));
        return Rows.All(row => withheld.TryGetValue(row.ProjectId, out HashSet<int>? restricted)
            && row.Revealed.All(i => columns.IndexOf(Columns[i]) is var at and >= 0 && !restricted.Contains(at)));
    }

    /// <summary>The columns a project's rows withhold from the caller now: RESTRICTED by scope or classification, or masked.</summary>
    private static IEnumerable<int> Withheld(IEnumerable<ReportRowData> rows) =>
        rows.SelectMany(r => r.Cells.Select((cell, i) => (cell, i))).Where(x => x.cell.UnknownReason == Contracts.ReportUnknownReason.Restricted || x.cell.IsMasked).Select(x => x.i);

    private static List<int> Revealed(IEnumerable<ReportRowData> rows) =>
        [.. rows.SelectMany(r => r.Cells.Select((cell, i) => (cell, i))).Where(x => ReportExecution.IsRevealed(x.cell)).Select(x => x.i).Distinct().Order()];
}
