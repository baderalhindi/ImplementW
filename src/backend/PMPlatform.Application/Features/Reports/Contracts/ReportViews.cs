using PMPlatform.Application.Common.Projections;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>What a report cell holds (FG-02 §6.2): a projection field's type, or a project's own identity (text, or a department or entity).</summary>
public enum ReportValueType
{
    /// <summary>A source-owned state from a closed set.</summary>
    Code = 1,

    /// <summary>Free text as entered, or an identifier.</summary>
    Text = 2,

    /// <summary>A department or an external entity, by id, with its name.</summary>
    Reference = 3,

    Count = 4,
    Percent = 5,
    Days = 6,

    /// <summary>Money in SAR, an exact two-place decimal string (R-16).</summary>
    Sar = 7,

    Boolean = 8,
    Date = 9,
    DateTime = 10,
}

/// <summary>Why a cell or a section holds no value (FG-02 §7.1): never 0, never a colour.</summary>
public enum ReportUnknownReason
{
    /// <summary>The source holds no value.</summary>
    Missing = 1,

    /// <summary>The value does not apply to this project.</summary>
    NotApplicable = 2,

    /// <summary>The caller may not see it: their scope, or a classification or masking rule (ADR-010).</summary>
    Restricted = 3,

    /// <summary>The source failed to answer; the rest of the report stands (FG-02 §26).</summary>
    SourceUnavailable = 4,
}

/// <summary>A report the caller may run (SCR-130's catalogue).</summary>
public sealed record ReportCatalogueEntry(
    ReportCode Code, Guid ReportDefinitionId, int VersionNo, BilingualLabel Name, BilingualLabel? Description, ReportAudienceFamily AudienceFamily, bool AllowsSavedViews);

public sealed record ReportCataloguePage(IReadOnlyList<ReportCatalogueEntry> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// A report's PUBLISHED version as the caller may run it (RPT-API-002, -003, -054): its columns, its parameters with the options the caller
/// may choose, the export formats (ADR-005), and what the caller may do with it now.
/// </summary>
public sealed record ReportView(
    ReportCode Code,
    Guid ReportDefinitionId,
    int VersionNo,
    BilingualLabel Name,
    BilingualLabel? Description,
    ReportAudienceFamily AudienceFamily,
    bool AllowsSavedViews,
    IReadOnlyList<ReportColumnView> Columns,
    IReadOnlyList<ReportParameterView> Parameters,
    IReadOnlyList<ReportExportFormat> ExportFormats,
    bool MayExport,
    bool MaySaveView);

/// <summary>
/// A column: <see cref="SourceEntityCode"/> and <see cref="FieldCode"/> name it as configuration does; <see cref="ProjectionCode"/> and
/// <see cref="SemanticState"/> are its registered projection's (null for the project's own identity).
/// </summary>
public sealed record ReportColumnView(
    string SourceEntityCode,
    string FieldCode,
    BilingualLabel Label,
    ReportValueType ValueType,
    string? ProjectionCode,
    ProjectionSemanticState? SemanticState,
    bool IsDefaultVisible,
    bool IsSensitive);

/// <summary>A parameter and the values the caller may give it: never a value they may not know (BR-RPT-010).</summary>
public sealed record ReportParameterView(string Code, BilingualLabel Label, ReportParameterDataType DataType, bool IsRequired, IReadOnlyList<ReportParameterOptionView> Options);

/// <summary>An option: a value the parameter takes and its label; <see cref="CatalogueEntryReference"/> names the FG-02 catalogue entry it absorbs.</summary>
public sealed record ReportParameterOptionView(string Value, BilingualLabel Label, string? CatalogueEntryReference);

/// <summary>
/// One page of a report's rows, run now over the caller's authorised population (FG-02 §4.3). Every cell is a projection's value with its own
/// freshness and as-of, or UNKNOWN with its reason; <see cref="Sections"/> carry each projection's semantic state, freshness and coverage
/// (RPT-CC-08 to -12). <see cref="GeneratedAt"/> is when the report was run and is never a source's as-of (BR-RPT-030).
/// </summary>
public sealed record ReportResultPage(
    ReportCode? ReportCode,
    int? VersionNo,
    IReadOnlyList<ReportColumnView> Columns,
    IReadOnlyList<ReportSection> Sections,
    IReadOnlyList<ReportRow> Items,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<Guid> DepartmentOptions,
    DateTimeOffset GeneratedAt);

/// <summary>A row: the project it is about, the snapshot's place for a historical report, and one cell per column in the columns' order.</summary>
public sealed record ReportRow(Guid ProjectId, int? SnapshotIndex, IReadOnlyList<ReportCell> Cells);

/// <summary>
/// One value. <see cref="Value"/> is the source's own (a code, an exact decimal string, an ISO date or an id); <see cref="Label"/> names a
/// department or an entity. A cell with no value has <see cref="UnknownReason"/>; <see cref="IsMasked"/> marks one withheld by ADR-010.
/// </summary>
public sealed record ReportCell(
    string? Value, BilingualLabel? Label, ProjectionFreshness Freshness, DateTimeOffset? AsOf, ReportUnknownReason? UnknownReason, bool IsMasked);

/// <summary>A projection's Coverage/Freshness banner over the rows (FG-02 §7): the R-20(c) metadata and the populations behind it.</summary>
public sealed record ReportSection(
    string ProjectionCode, string SourceDomain, string ProjectionVersion, ProjectionMeta Projection, ReportUnknownReason? UnknownReason, ReportCoverage Coverage);

/// <summary>FG-02 §7.2: the projects a projection expected a value from, those shown, those left out and those whose value is stale.</summary>
public sealed record ReportCoverage(int EligibleCount, int IncludedCount, int ExcludedCount, int StaleCount);
