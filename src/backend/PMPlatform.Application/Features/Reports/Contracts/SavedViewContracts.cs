using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>
/// A saved view as its owner writes it, whole (MOD-062): a report's parameter values, or an SCR-138 composition of allowlisted columns (with
/// their sorts) and filters, and the roles it is shared with — none for a private view. Configuration only (BR-RPT-031).
/// </summary>
public sealed record SavedViewInput(
    NarrativeText Name,
    SavedViewType ViewType,
    ReportCode? ReportCode,
    IReadOnlyList<ReportParameterInput> Parameters,
    IReadOnlyList<SavedViewColumnInput> Columns,
    IReadOnlyList<ReportFilterInput> Filters,
    IReadOnlyList<string> SharedWithRoleCodes);

public sealed record SavedViewColumnInput(string SourceEntityCode, string FieldCode, ReportSortDirection? SortDirection);

/// <summary>Whether a saved view still fits the report version and the allowlist in force (SAV-012); checked at every read and run.</summary>
public enum SavedViewCompatibility
{
    Valid = 1,
    Incompatible = 2,
}

/// <summary>
/// A saved view (RPT-API-020): whose it is, what it holds, the report version it was saved against, and whether it fits what is in force now for
/// the reader — with each thing that no longer fits, never silently reinterpreted (SCR-139). <see cref="SharedWithRoleCodes"/> is the owner's to
/// read: a person it is shared with reads it empty.
/// </summary>
public sealed record SavedViewDetail(
    Guid Id,
    Guid OwnerUserId,
    SavedViewType ViewType,
    NarrativeText Name,
    ReportCode? ReportCode,
    Guid? ReportDefinitionId,
    int? ReportVersionNo,
    IReadOnlyList<ReportParameterInput> Parameters,
    IReadOnlyList<SavedViewColumnInput> Columns,
    IReadOnlyList<ReportFilterInput> Filters,
    IReadOnlyList<string> SharedWithRoleCodes,
    SavedViewCompatibility Compatibility,
    IReadOnlyList<SavedViewIssue> Issues,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>What no longer fits: the field of the view, and why.</summary>
public sealed record SavedViewIssue(string Field, string Code);

/// <summary>A view in SCR-139's list: its owner, the roles it is shared with (the owner's to read, as on the detail), and whether it fits now.</summary>
public sealed record SavedViewSummary(
    Guid Id,
    Guid OwnerUserId,
    SavedViewType ViewType,
    NarrativeText Name,
    ReportCode? ReportCode,
    IReadOnlyList<string> SharedWithRoleCodes,
    SavedViewCompatibility Compatibility,
    DateTimeOffset UpdatedAt);

public sealed record SavedViewPage(IReadOnlyList<SavedViewSummary> Items, int Page, int PageSize, int TotalCount);
