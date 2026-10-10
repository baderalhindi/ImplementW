using PMPlatform.Application.Common.Projections;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards.Contracts;

/// <summary>One version of a governed dashboard, whole (ADM-036): its lifecycle, audience and widgets.</summary>
public sealed record DashboardDefinitionDetail(
    Guid Id,
    DashboardCode Code,
    int VersionNo,
    BilingualLabel Name,
    BilingualLabel? Description,
    bool AllowsPersonalization,
    GovernedLifecycleState LifecycleState,
    Guid? ValidatedByUserId,
    DateTimeOffset? ValidatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? RetiredAt,
    IReadOnlyList<DashboardAudienceDetail> Audience,
    IReadOnlyList<DashboardWidgetDetail> Widgets,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record DashboardAudienceDetail(Guid RoleId, string RoleCode, bool IsDefaultLanding);

public sealed record DashboardWidgetDetail(
    Guid Id,
    string Code,
    BilingualLabel Title,
    DashboardWidgetType WidgetType,
    string SourceProjectionCode,
    Guid? DataClassificationItemId,
    bool IsOptionalVisibility,
    short LayoutRow,
    short LayoutColumn,
    short LayoutSpan);

public sealed record DashboardDefinitionSummary(
    Guid Id, DashboardCode Code, int VersionNo, BilingualLabel Name, GovernedLifecycleState LifecycleState, DateTimeOffset? PublishedAt, DateTimeOffset? RetiredAt, DateTimeOffset UpdatedAt);

public sealed record DashboardDefinitionPage(IReadOnlyList<DashboardDefinitionSummary> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// A registered source projection (FG-01 §4.1, PRJ-001–024): the only thing a widget may bind to. It is owned by its source domain;
/// FG-01 reads it through that domain's contracts and never its tables (CFA-DSH-006). <see cref="PermissionCode"/> is the source's
/// own view permission, decided on each project; <see cref="IsSensitive"/> marks figures masked by audience (ADR-010).
/// </summary>
public sealed record DashboardProjectionDetail(
    string Code,
    string SourceDomain,
    string Version,
    ProjectionSemanticState SemanticState,
    IReadOnlyList<DashboardContextKind> Contexts,
    IReadOnlyList<DashboardWidgetType> WidgetTypes,
    string PermissionCode,
    bool IsSensitive,
    string BusinessMeaning,
    string? DrillTargetScreenId);

public sealed record DashboardProjectionPage(IReadOnlyList<DashboardProjectionDetail> Items, int Page, int PageSize, int TotalCount);
