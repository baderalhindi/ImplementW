using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;

using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Dashboards.Contracts;

/// <summary>
/// A DRAFT version's content, as a whole (R-5): its labels, whether it is personalisable, its audience and its widgets. Nothing in
/// it is an expression: a widget names a registered projection by code (BR-DSH-030).
/// </summary>
public sealed record DashboardDefinitionContent(
    BilingualLabel Name,
    BilingualLabel? Description,
    bool AllowsPersonalization,
    IReadOnlyList<DashboardAudienceInput> Audience,
    IReadOnlyList<DashboardWidgetInput> Widgets);

public sealed record DashboardAudienceInput(string RoleCode, bool IsDefaultLanding);

public sealed record DashboardWidgetInput(
    string Code,
    BilingualLabel Title,
    DashboardWidgetType WidgetType,
    string SourceProjectionCode,
    Guid? DataClassificationItemId,
    bool IsOptionalVisibility,
    short LayoutRow,
    short LayoutColumn,
    short LayoutSpan);

/// <summary>
/// The context a dashboard is read in: the project of the Project Dashboard (context-bound, DSH-CC-18), or the filters of the others — the
/// department (sector), the delivering external entity, the region and the city — each narrowing the population, never widening it.
/// </summary>
public sealed record DashboardContext(Guid? ProjectId, Guid? DepartmentId, Guid? ExternalEntityId = null, Guid? RegionItemId = null, Guid? CityItemId = null)
{
    /// <summary>The sector: the project classification (the 2 Nov 2026 review aligns the catalogue to RPMO's 15 sectors).</summary>
    public Guid? ClassificationItemId { get; init; }

    public ProjectLifecycleState? Status { get; init; }

    public Guid? PriorityItemId { get; init; }

    public Guid? StrategicPortfolioItemId { get; init; }

    public Guid? StrategicInitiativeItemId { get; init; }

    /// <summary>A year: the projects whose planned period touches it.</summary>
    public int? PeriodYear { get; init; }

    public bool IsFiltered => DepartmentId is not null || ExternalEntityId is not null || RegionItemId is not null || CityItemId is not null
                              || ClassificationItemId is not null || Status is not null || PriorityItemId is not null
                              || StrategicPortfolioItemId is not null || StrategicInitiativeItemId is not null || PeriodYear is not null;
}

/// <summary>ADR-019: the caller's choices for the optional widgets of a personalisable dashboard, as a whole; a widget not named keeps the governed layout.</summary>
public sealed record DashboardPersonalizationInput(IReadOnlyList<WidgetPersonalizationInput> Widgets);

public sealed record WidgetPersonalizationInput(string WidgetCode, bool IsHidden, short? SortOrder);

public sealed record DashboardDefinitionQuery(DashboardCode? Code, GovernedLifecycleState? LifecycleState);
