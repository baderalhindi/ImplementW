using PMPlatform.Application.Common.Projections;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Dashboards.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 30, Reports → Dashboards (query; TASK-071): the registered source projections FG-02 reads, as rows. A report row
/// is one project — or one source snapshot of a project for a historical projection — and each of its cells is one field of one registered
/// projection, read through the same adapter, the same authorization and the same semantics as a dashboard widget (FG-02 §4.1: "the FG-01
/// governed projection contract is reused; raw-table reporting is prohibited"). Nothing here recalculates a source-owned value (M-12).
/// </summary>
public interface IProjectionRowReader
{
    /// <summary>The projections a report may read, by code: every registered projection about projects, with its fields.</summary>
    public IReadOnlyList<ProjectionDescriptor> Projections { get; }

    /// <summary>
    /// The rows the caller may see. The population is the projects the caller reaches under the base projection's permission and under every
    /// one of <see cref="ProjectionRowQuery.RowPermissionCodes"/>, that the base projection expects a value from; each cell is then
    /// authorised on its own projection's permission and the column's classification, so a row can hold cells the caller may not see
    /// (RESTRICTED, with no value). Authorization is applied before any source is read (FG-02 §8.2, RPT-CC-06).
    /// </summary>
    public Task<ProjectionRowSet> ReadAsync(ProjectionRowQuery query, CancellationToken cancellationToken);
}

/// <summary>What a report cell holds, as its projection states it (FG-02 §6.2 data type).</summary>
public enum ProjectionValueType
{
    /// <summary>A source-owned state or condition from a closed set: a lifecycle state, a health, a reporting status.</summary>
    Code = 1,

    /// <summary>A count the source makes, as an integer string.</summary>
    Count = 2,

    /// <summary>A percentage as an exact decimal string.</summary>
    Percent = 3,

    /// <summary>A number of days as an integer string.</summary>
    Days = 4,

    /// <summary>Money in SAR as an exact two-place decimal string (R-16).</summary>
    Sar = 5,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean = 6,

    /// <summary>A date, <c>YYYY-MM-DD</c> (R-15).</summary>
    Date = 7,

    /// <summary>A UTC timestamp, RFC 3339 with <c>Z</c> (R-15).</summary>
    DateTime = 8,
}

/// <summary>Whether a projection's rows are one per project, or one per source snapshot of a project (a source-held history, BR-RPT-029).</summary>
public enum ProjectionGrain
{
    Project = 1,
    Snapshot = 2,
}

/// <summary>
/// A field of a registered projection (FG-02 §4.1 Measures and Dimensions). <see cref="Values"/> is the closed set of a
/// <see cref="ProjectionValueType.Code"/> field, in the source's order; empty otherwise. <see cref="IsSensitive"/> marks a financial amount
/// (ADR-010).
/// </summary>
public sealed record ProjectionFieldDescriptor(string Code, ProjectionValueType Type, bool IsSensitive, IReadOnlyList<string> Values);

/// <summary>
/// A registered projection as a report reads it. <see cref="EntityCode"/> is its code without the dot — the name configuration uses
/// for it, since a configuration code is one word (REPORT_RULES' <c>sourceEntityCode</c>).
/// </summary>
public sealed record ProjectionDescriptor(
    string Code,
    string EntityCode,
    string SourceDomain,
    string Version,
    ProjectionSemanticState SemanticState,
    string PermissionCode,
    bool IsSensitive,
    ProjectionGrain Grain,
    string BusinessMeaning,
    IReadOnlyList<ProjectionFieldDescriptor> Fields);

/// <summary>One column: a field of a registered projection, and the DATA_CLASSIFICATION item a caller must be cleared for to see it (ADR-010).</summary>
public sealed record ProjectionColumn(string ProjectionCode, string FieldCode, Guid? DataClassificationItemId);

/// <summary>
/// A read of rows. <see cref="ProjectIds"/> and <see cref="DepartmentId"/> narrow the population and never widen it: a department outside the
/// caller's options yields no rows and <see cref="ProjectionRowSet.DepartmentOffered"/> false. <see cref="MaskSensitiveFields"/> withholds every
/// sensitive field whatever the caller's grants (ADR-013's amendment to TASK-071: an entity's reports have financial fields masked).
/// <see cref="IncludeIneligible"/> keeps a reached project the base projection expects no value from, as a row of NOT_APPLICABLE cells: what
/// a download's re-authorization reads, where a project's state may have moved since and only its authorization counts.
/// </summary>
public sealed record ProjectionRowQuery(
    Guid CallerId,
    string BaseProjectionCode,
    IReadOnlyList<string> RowPermissionCodes,
    IReadOnlyList<ProjectionColumn> Columns,
    IReadOnlyCollection<Guid>? ProjectIds,
    Guid? DepartmentId,
    bool MaskSensitiveFields,
    DateTimeOffset Now,
    bool IncludeIneligible = false);

/// <summary>
/// The rows, in project order (and snapshot order within a project), with one section per projection read. <see cref="DepartmentOptions"/> are
/// the departments of the population before any department filter: the only values that filter takes (BR-RPT-010).
/// </summary>
public sealed record ProjectionRowSet(bool DepartmentOffered, IReadOnlyList<Guid> DepartmentOptions, IReadOnlyList<ProjectionRow> Rows, IReadOnlyList<ProjectionSection> Sections);

/// <summary>
/// One row: the project it is about — identified because the caller reaches it under the base projection — and one cell per column, in the
/// query's order. <see cref="SnapshotIndex"/> numbers a snapshot-grain row within its project, oldest first; null for a project-grain row.
/// </summary>
public sealed record ProjectionRow(
    Guid ProjectId, string? FormalProjectId, NarrativeText Title, Guid DepartmentId, Guid? ExternalEntityId, int? SnapshotIndex, IReadOnlyList<ProjectionCell> Cells);

/// <summary>
/// One value as its source states it. A cell with a value is FRESH or STALE (a stale value keeps its own as-of, BR-RPT-020). A cell without
/// one is UNKNOWN with its reason — missing is never 0 (BR-RPT-019); <see cref="IsMasked"/> marks a value withheld from the caller's audience
/// (ADR-010), which is RESTRICTED.
/// </summary>
public sealed record ProjectionCell(string? Value, ProjectionFreshness Freshness, DateTimeOffset? AsOf, WidgetUnknownReason? UnknownReason, bool IsMasked)
{
    public static ProjectionCell Unknown(WidgetUnknownReason reason, bool isMasked = false) => new(null, ProjectionFreshness.Unknown, null, reason, isMasked);

    /// <summary>Whether the caller was shown a value: one present and not withheld.</summary>
    public bool IsRevealed => UnknownReason is null && !IsMasked;
}

/// <summary>
/// The Coverage/Freshness banner of one projection over the rows (FG-02 §7.1, §7.2): its semantic state, its least current as-of, whether any
/// value is stale, and the eligible, included and excluded populations. <see cref="UnknownReason"/> is set when no value could be shown.
/// </summary>
public sealed record ProjectionSection(
    string ProjectionCode, string SourceDomain, string Version, ProjectionMeta Projection, WidgetUnknownReason? UnknownReason, WidgetCoverage Coverage);
