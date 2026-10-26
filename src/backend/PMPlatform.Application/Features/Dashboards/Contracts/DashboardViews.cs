using PMPlatform.Application.Common.Projections;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards.Contracts;

/// <summary>Whether a dashboard is read for one project (DSH-009 in SCR-040, DSH-008) or over the population of projects the caller may see.</summary>
public enum DashboardContextKind
{
    Project = 1,
    Portfolio = 2,
}

/// <summary>A PUBLISHED dashboard the caller's roles may open, and whether it is where they land (Blueprint §20.2; the role-aware Home of FG-01 §5.1).</summary>
public sealed record DashboardCatalogueEntry(
    DashboardCode Code,
    Guid DashboardDefinitionId,
    int VersionNo,
    BilingualLabel Name,
    BilingualLabel? Description,
    DashboardContextKind ContextKind,
    bool AllowsPersonalization,
    bool IsDefaultLanding);

public sealed record DashboardCataloguePage(IReadOnlyList<DashboardCatalogueEntry> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// A dashboard as the caller reads it now: the PUBLISHED version's composition with the caller's personalisation applied, and each
/// widget's result. <see cref="RefreshedAt"/> is when FG-01 assembled it and is never a source's as-of (BR-DSH-042).
/// <see cref="DepartmentOptions"/> are the departments of the projects the caller may see here, the only values the department
/// filter takes (DSH-CC-17).
/// </summary>
public sealed record DashboardView(
    DashboardCode Code,
    Guid DashboardDefinitionId,
    int VersionNo,
    BilingualLabel Name,
    BilingualLabel? Description,
    DashboardContextKind ContextKind,
    bool AllowsPersonalization,
    Guid? ProjectId,
    Guid? DepartmentId,
    IReadOnlyList<Guid> DepartmentOptions,
    DateTimeOffset RefreshedAt,
    IReadOnlyList<DashboardWidgetResult> Widgets);

/// <summary>
/// One widget's result (FG-01 §10.1, RUN-001–022): what it presents, from which projection and version, in which semantic state, as
/// of when, how fresh and how complete (<see cref="Projection"/>, the R-20(c) contract). When the source cannot say —
/// <see cref="ProjectionFreshness.Unknown"/> — <see cref="UnknownReason"/> says why and <see cref="Data"/> is null: missing is never
/// 0, unknown never a colour (BR-DSH-013 to -015). A STALE value keeps its data and its original as-of (BR-DSH-041).
/// </summary>
public sealed record DashboardWidgetResult(
    string Code,
    BilingualLabel Title,
    DashboardWidgetType WidgetType,
    string ProjectionCode,
    string ProjectionVersion,
    string SourceDomain,
    short LayoutRow,
    short LayoutColumn,
    short LayoutSpan,
    bool IsOptionalVisibility,
    bool IsHidden,
    short? PersonalSortOrder,
    ProjectionMeta Projection,
    WidgetUnknownReason? UnknownReason,
    WidgetData? Data,
    WidgetCoverage? CoverageDetail,
    IReadOnlyList<string> MaskedFields,
    string? DrillTargetScreenId);

/// <summary>Why a widget's freshness is UNKNOWN (FG-01 §7.3, §21.1).</summary>
public enum WidgetUnknownReason
{
    /// <summary>The source holds no value: "Missing/No Data", never 0.</summary>
    Missing = 1,

    /// <summary>The value does not apply here, e.g. a project not yet reporting, or no project to count; excluded, never 0.</summary>
    NotApplicable = 2,

    /// <summary>The caller may not see this widget's data here (FG-03 scope, ADR-010 classification); nothing about it is disclosed.</summary>
    Restricted = 3,

    /// <summary>The source failed to answer; other widgets are unaffected (BR-DSH-040).</summary>
    SourceUnavailable = 4,
}

/// <summary>
/// The value a widget presents, as the source owns it: a categorical <see cref="State"/> (e.g. Overall Project Health), measured
/// <see cref="Figures"/>, a <see cref="Distribution"/> counted by a source-owned dimension, or a source-provided <see cref="Series"/>.
/// </summary>
public sealed record WidgetData(string? State, IReadOnlyList<WidgetFigure> Figures, IReadOnlyList<WidgetBucket> Distribution, IReadOnlyList<WidgetSeriesPoint> Series);

/// <summary>
/// One measure: its value as an exact decimal string (R-16: never a binary float), its unit, and whether the caller's audience may
/// see it. A masked figure has no value (ADR-010, R-20(b)); an absent one is null.
/// </summary>
public sealed record WidgetFigure(string Measure, string? Value, string Unit, bool IsMasked);

/// <summary>A count by a source-owned value: a health, a rating, a lifecycle state. <see cref="Label"/> is the source's own when it has one (a risk rating).</summary>
public sealed record WidgetBucket(string Key, BilingualLabel? Label, int Count);

/// <summary>A point of a source-provided history: the period it covers, when it was current, and its value then (BR-DSH-018).</summary>
public sealed record WidgetSeriesPoint(DateTimeOffset AsOf, DateOnly? PeriodStart, DateOnly? PeriodEnd, string? State, IReadOnlyList<WidgetFigure> Figures);

/// <summary>
/// FG-01 §7.4: the projects an aggregate expected, those it counted and those it left out, and why. <see cref="StaleCount"/> are
/// counted projects whose source value is out of date; any makes the aggregate STALE.
/// </summary>
public sealed record WidgetCoverage(int EligibleCount, int IncludedCount, int ExcludedCount, int StaleCount, IReadOnlyList<CoverageExclusion> Exclusions);

public sealed record CoverageExclusion(CoverageExclusionReason Reason, int Count);

/// <summary>Why an eligible project is left out of an aggregate (FG-01 §7.4 ExclusionReasonSummary).</summary>
public enum CoverageExclusionReason
{
    /// <summary>The source holds no value for it.</summary>
    Missing = 1,

    /// <summary>Its value is withheld from the caller's audience (ADR-010).</summary>
    Masked = 2,

    /// <summary>Its value cannot be combined with the others, e.g. a currency not verified as SAR (ADR-008).</summary>
    Incompatible = 3,
}

/// <summary>ADR-019: the caller's presentation choices on a personalisable dashboard.</summary>
public sealed record DashboardPersonalizationDetail(DashboardCode Code, Guid DashboardDefinitionId, IReadOnlyList<WidgetPersonalizationDetail> Widgets);

public sealed record WidgetPersonalizationDetail(string WidgetCode, bool IsHidden, short? SortOrder);
