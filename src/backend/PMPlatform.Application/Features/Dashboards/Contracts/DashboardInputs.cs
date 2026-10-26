using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;

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

/// <summary>The context a dashboard is read in: the project of the Project Dashboard (context-bound, DSH-CC-18), or a department filter of the others.</summary>
public sealed record DashboardContext(Guid? ProjectId, Guid? DepartmentId);

/// <summary>ADR-019: the caller's choices for the optional widgets of a personalisable dashboard, as a whole; a widget not named keeps the governed layout.</summary>
public sealed record DashboardPersonalizationInput(IReadOnlyList<WidgetPersonalizationInput> Widgets);

public sealed record WidgetPersonalizationInput(string WidgetCode, bool IsHidden, short? SortOrder);

public sealed record DashboardDefinitionQuery(DashboardCode? Code, GovernedLifecycleState? LifecycleState);
