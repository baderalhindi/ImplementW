using PMPlatform.Application.Common.Projections;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>A row of a run: the project, the snapshot's place, the project's identity for the default order, and one cell per planned column.</summary>
internal sealed record ReportRowData(Guid ProjectId, int? SnapshotIndex, string? FormalProjectId, string Title, IReadOnlyList<ReportCell> Cells);

/// <summary>
/// A plan run for one caller: every row it may see, filtered and ordered; each projection's banner; the departments the caller may choose; and
/// whether the project or department the plan was narrowed to is one the caller may know.
/// </summary>
internal sealed record ReportExecution(
    ReportPlan Plan, IReadOnlyList<ReportRowData> Rows, IReadOnlyList<ReportSection> Sections, IReadOnlyList<Guid> DepartmentOptions, bool IsNarrowingAuthorized,
    DateTimeOffset GeneratedAt)
{
    /// <summary>Whether a value the caller was shown is a financial amount or of a classified column (FG-02 §9.3: the output's classification).</summary>
    public bool RevealsSensitive => Rows.Any(r => Plan.Columns.Select((c, i) => (c, i))
        .Any(x => (x.c.Field.IsSensitive || x.c.ClassificationId is not null) && IsRevealed(r.Cells[x.i])));

    /// <summary>The least current as-of of the values shown; null when none is.</summary>
    public DateTimeOffset? SourceAsOf => Rows.SelectMany(r => r.Cells).Where(IsRevealed).Min(c => c.AsOf);

    public static bool IsRevealed(ReportCell cell) => cell.UnknownReason is null && !cell.IsMasked && cell.Value is not null;
}

/// <summary>
/// Runs a <see cref="ReportPlan"/> (FG-02 §21 Report Query Service). Rows and cells come from FG-01's register through edge 30, authorised there
/// before any source is read; this adds the project's own identity, names departments and entities, keeps the rows the plan's filters accept —
/// only on values the caller is shown — and orders them deterministically. An external caller's financial amounts are masked whatever their
/// grants (ADR-013's amendment). Nothing here computes a source's value.
/// </summary>
internal sealed class ReportExecutor(IProjectionRowReader reader, IOrganizationDirectory organizations, TimeProvider timeProvider)
{
    public async Task<ReportExecution> ExecuteAsync(
        Guid callerId, bool isExternal, ReportPlan plan, IReadOnlyList<string> rowPermissions, IReadOnlyCollection<Guid>? projectIds, CancellationToken cancellationToken,
        bool includeIneligible = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<int> projected = [.. plan.Columns.Select((c, i) => (c, i)).Where(x => !x.c.Field.IsIdentity).Select(x => x.i)];
        ProjectionRowSet set = await reader.ReadAsync(
            new ProjectionRowQuery(
                callerId,
                plan.Basis.Code,
                rowPermissions,
                [.. projected.Select(i => new ProjectionColumn(plan.Columns[i].Field.Projection!.Code, plan.Columns[i].Field.FieldCode, plan.Columns[i].ClassificationId))],
                projectIds ?? (plan.ProjectId is { } projectId ? [projectId] : null),
                plan.DepartmentId,
                isExternal,
                now,
                includeIneligible),
            cancellationToken).ConfigureAwait(false);

        OrganizationNames names = await organizations.ListNamesAsync(
            [.. set.Rows.Select(r => r.DepartmentId).Distinct()], [.. set.Rows.Select(r => r.ExternalEntityId).OfType<Guid>().Distinct()], cancellationToken).ConfigureAwait(false);
        List<ReportRowData> rows = [.. set.Rows.Select(row => new ReportRowData(row.ProjectId, row.SnapshotIndex, row.FormalProjectId, row.Title.Text,
            [.. plan.Columns.Select((column, i) => column.Field.IsIdentity ? Identity(column.Field, row, names, now) : Cell(row.Cells[projected.IndexOf(i)]))]))];
        rows = [.. rows.Where(r => plan.Filters.All(f => ReportValues.Matches(plan.Columns[f.Column].Field, r.Cells[f.Column], f.Operator, f.Values)))];
        rows.Sort((a, b) => Compare(plan, a, b));

        // A project or department the plan was narrowed to that the caller cannot know yields no population at all (BR-RPT-010).
        bool authorized = set.DepartmentOffered && (plan.ProjectId is null || projectIds is not null || set.DepartmentOptions.Count > 0);
        return new ReportExecution(plan, rows, [.. set.Sections.Select(Section)], set.DepartmentOptions, authorized, now);
    }

    public static ReportResultPage Page(ReportExecution execution, IReadOnlyList<ReportColumnView> columns, ReportCode? code, int? versionNo, PageRequest page)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(page);
        List<int> shown = [.. execution.Plan.ShownColumns];
        return new ReportResultPage(
            code,
            versionNo,
            columns,
            execution.Sections,
            [.. execution.Rows.Skip(page.Skip).Take(page.PageSize).Select(r => new ReportRow(r.ProjectId, r.SnapshotIndex, [.. shown.Select(i => r.Cells[i])]))],
            page.Page,
            page.PageSize,
            execution.Rows.Count,
            execution.DepartmentOptions,
            execution.GeneratedAt);
    }

    /// <summary>The column view of a planned column, labelled as the report or the allowlist labels it.</summary>
    public static ReportColumnView View(PlannedColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return new ReportColumnView(
            column.Field.SourceEntityCode, column.Field.FieldCode, column.Label, column.Field.Type, column.Field.Projection?.Code, column.Field.Projection?.SemanticState,
            column.IsDefaultVisible, column.Field.IsSensitive);
    }

    public static ReportUnknownReason Reason(WidgetUnknownReason reason) => reason switch
    {
        WidgetUnknownReason.Missing => ReportUnknownReason.Missing,
        WidgetUnknownReason.NotApplicable => ReportUnknownReason.NotApplicable,
        WidgetUnknownReason.Restricted => ReportUnknownReason.Restricted,
        WidgetUnknownReason.SourceUnavailable => ReportUnknownReason.SourceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown reason."),
    };

    private static ReportCell Cell(ProjectionCell cell) =>
        new(cell.Value, null, cell.Freshness, cell.AsOf, cell.UnknownReason is { } reason ? Reason(reason) : null, cell.IsMasked);

    /// <summary>The project's own identity, read with the row from WF-01 now: shown on every row the caller reaches.</summary>
    private static ReportCell Identity(ReportField field, ProjectionRow row, OrganizationNames names, DateTimeOffset now) => field.FieldCode switch
    {
        ReportFields.FormalProjectId when row.FormalProjectId is { } id => Known(id, null, now),
        ReportFields.Title => Known(row.Title.Text, null, now),
        ReportFields.Department => Known(row.DepartmentId.ToString(), names.Departments.GetValueOrDefault(row.DepartmentId), now),
        ReportFields.ExternalEntity when row.ExternalEntityId is { } entity => Known(entity.ToString(), names.ExternalEntities.GetValueOrDefault(entity), now),
        ReportFields.FormalProjectId or ReportFields.ExternalEntity => new ReportCell(null, null, ProjectionFreshness.Unknown, null, ReportUnknownReason.NotApplicable, false),
        _ => throw new InvalidOperationException($"{field.Key} is not an identity field."),
    };

    private static ReportCell Known(string value, BilingualLabel? label, DateTimeOffset now) => new(value, label, ProjectionFreshness.Fresh, now, null, false);

    private static ReportSection Section(ProjectionSection section) =>
        new(section.ProjectionCode, section.SourceDomain, section.Version, section.Projection, section.UnknownReason is { } reason ? Reason(reason) : null,
            new ReportCoverage(section.Coverage.EligibleCount, section.Coverage.IncludedCount, section.Coverage.ExcludedCount, section.Coverage.StaleCount));

    /// <summary>The plan's sorts, then the Formal Project ID, the title, the project and the snapshot: the same order on every run (US-RPT-SYS-020).</summary>
    private static int Compare(ReportPlan plan, ReportRowData a, ReportRowData b)
    {
        foreach (PlannedSort sort in plan.Sort)
        {
            int byValue = ReportValues.CompareCells(plan.Columns[sort.Column].Field, a.Cells[sort.Column], b.Cells[sort.Column]);
            bool aKnown = a.Cells[sort.Column].UnknownReason is null;
            bool bKnown = b.Cells[sort.Column].UnknownReason is null;
            if (byValue != 0)
            {
                // A cell without a value stays last whatever the direction.
                return sort.Direction == ReportSortDirection.Desc && aKnown && bKnown ? -byValue : byValue;
            }
        }

        int c = (a.FormalProjectId, b.FormalProjectId) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => string.CompareOrdinal(a.FormalProjectId, b.FormalProjectId),
        };
        return c != 0 ? c
            : string.CompareOrdinal(a.Title, b.Title) is var t and not 0 ? t
            : a.ProjectId.CompareTo(b.ProjectId) is var p and not 0 ? p
            : (a.SnapshotIndex ?? 0).CompareTo(b.SnapshotIndex ?? 0);
    }
}
