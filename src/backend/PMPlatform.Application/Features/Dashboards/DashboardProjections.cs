using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Projections;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Domain.Dashboards;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>Which of a population's projects a projection expects a value from; the others are outside its denominator (BR-DSH-016).</summary>
internal enum ProjectionEligibility
{
    /// <summary>Every project, in any lifecycle state.</summary>
    AnyProject = 1,

    /// <summary>Projects that have been activated — ACTIVE, SUSPENDED, COMPLETED or CLOSED — and so report delivery.</summary>
    Delivering = 2,

    /// <summary>ACTIVE projects only: a reporting obligation runs while the project does.</summary>
    Reporting = 3,

    /// <summary>Not about projects: FG-01's own configuration.</summary>
    NoProject = 4,
}

/// <summary>
/// A registered source projection (FG-01 §4.1): its stable code, owning domain and contract version, the one semantic state it
/// presents, the contexts and widget types it supports, the source's own view permission every value is authorised on, and whether
/// its figures are sensitive (ADR-010). Versions are this build's: a change to what a projection means is a new version and a
/// revision of this register (FG-01 §13.5, BR-DSH-035).
/// </summary>
internal sealed record ProjectionContract(
    string Code,
    string SourceDomain,
    string Version,
    ProjectionSemanticState SemanticState,
    IReadOnlyList<DashboardContextKind> Contexts,
    IReadOnlyList<DashboardWidgetType> WidgetTypes,
    string PermissionCode,
    bool IsSensitive,
    ProjectionEligibility Eligibility,
    string BusinessMeaning,
    string? DrillTargetScreenId)
{
    public bool Supports(DashboardContextKind context) => Contexts.Contains(context);

    public bool IsEligible(ProjectLifecycleState state) => Eligibility switch
    {
        ProjectionEligibility.AnyProject => true,
        ProjectionEligibility.Delivering => state is ProjectLifecycleState.Active or ProjectLifecycleState.Suspended or ProjectLifecycleState.Completed or ProjectLifecycleState.Closed,
        ProjectionEligibility.Reporting => state == ProjectLifecycleState.Active,
        ProjectionEligibility.NoProject => false,
        _ => throw new ArgumentOutOfRangeException(nameof(state), Eligibility, "Unknown eligibility."),
    };

    public DashboardProjectionDetail ToDetail() =>
        new(Code, SourceDomain, Version, SemanticState, Contexts, WidgetTypes, PermissionCode, IsSensitive, BusinessMeaning, DrillTargetScreenId);
}

/// <summary>
/// The projection register (FG-01 §4.2, Projection Registry of §24): every source a widget may bind to, and nothing else — ADM-036
/// binds only these codes, so no configuration can name a table, a query or a formula (BR-DSH-029, BR-DSH-030). Each is read
/// through its owning module's contracts (ADR-003 §8.2 edges 29 and 47), never its tables (CFA-DSH-006).
/// </summary>
internal static class DashboardProjections
{
    public const string ProjectLifecycleState = "PROJECT.LIFECYCLE_STATE";
    public const string ProjectHealthStatus = "PROGRESS.PROJECT_HEALTH_STATUS";
    public const string PublishedProgressSnapshot = "PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT";
    public const string PublishedProgressHistory = "PROGRESS.PUBLISHED_PROGRESS_HISTORY";
    public const string PublishedScheduleHealth = "PROGRESS.PUBLISHED_SCHEDULE_HEALTH";
    public const string ReportingCompleteness = "PROGRESS.REPORTING_COMPLETENESS";
    public const string ScheduleHealthStatus = "SCHEDULE.SCHEDULE_HEALTH_STATUS";
    public const string RiskExposure = "RISK.RISK_EXPOSURE";
    public const string FinancialPosition = "FINANCIAL_KPI.FINANCIAL_POSITION";
    public const string PublishedFinancialSnapshot = "FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT";
    public const string KpiCondition = "FINANCIAL_KPI.KPI_CONDITION";
    public const string DefinitionBacklog = "DASHBOARDS.DEFINITION_BACKLOG";

    private const string Version = "1";

    private static readonly DashboardContextKind[] Both = [DashboardContextKind.Project, DashboardContextKind.Portfolio];

    private static readonly DashboardWidgetType[] Categorical =
        [DashboardWidgetType.MetricCard, DashboardWidgetType.StatusDistribution, DashboardWidgetType.BarColumn, DashboardWidgetType.DonutPie];

    public static IReadOnlyList<ProjectionContract> All { get; } =
    [
        new(ProjectLifecycleState, "WF-01", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.ProjectView, false,
            ProjectionEligibility.AnyProject, "The project's lifecycle state as WF-01 holds it; over a population, the count of authorised projects in each state, each project once.", "SCR-025"),
        new(ProjectHealthStatus, "WF-02", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.ProgressView, false,
            ProjectionEligibility.Delivering, "Overall Project Health as WF-02 last computed it, with the live actual and planned percentages; never recomputed here (ICD-03).", "SCR-027"),
        new(PublishedProgressSnapshot, "WF-02", Version, ProjectionSemanticState.PublishedOfficial, Both, [.. Categorical, DashboardWidgetType.ProgressIndicator],
            PermissionCatalogue.ProgressView, false, ProjectionEligibility.Delivering,
            "The latest Published Progress Snapshot: the official Overall Project Health and progress percentages. Stale while a later reporting period is past due unpublished.", "SCR-040"),
        new(PublishedProgressHistory, "WF-02", Version, ProjectionSemanticState.HistoricalSnapshot, [DashboardContextKind.Project], [DashboardWidgetType.LineTrend],
            PermissionCatalogue.ProgressView, false, ProjectionEligibility.Delivering,
            "The project's immutable Published Progress Snapshots, oldest first: WF-02's own history, never rebuilt from current records.", "SCR-040"),
        new(PublishedScheduleHealth, "WF-02", Version, ProjectionSemanticState.PublishedOfficial, Both, Categorical, PermissionCatalogue.ProgressView, false,
            ProjectionEligibility.Delivering, "WF-03's Schedule Health as WF-02 published it in the latest snapshot: the schedule status an entity sees on its own project (ADR-013).", "SCR-040"),
        new(ReportingCompleteness, "WF-02", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.ProgressView, false,
            ProjectionEligibility.Reporting, "Whether an ACTIVE project's progress reporting is up to date: OVERDUE while a reporting period is past its due date unpublished. Not a measure of delay.", "SCR-040"),
        new(ScheduleHealthStatus, "WF-03", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.ScheduleView, false,
            ProjectionEligibility.Delivering, "Schedule Health as WF-03 last computed it against the Approved Baseline, with the finish variance in days; never re-derived from tasks.", "SCR-028"),
        new(RiskExposure, "WF-06", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.RiskView, false,
            ProjectionEligibility.Delivering, "Open risks counted by the rating their latest assessment recorded (WF-06), with those not yet assessed and those past review; never rescored.", "SCR-080"),
        new(FinancialPosition, "WF-14", Version, ProjectionSemanticState.CurrentLive, Both, [DashboardWidgetType.MetricCard], PermissionCatalogue.FinancialView, true,
            ProjectionEligibility.Delivering, "WF-14's live financial position: Approved Budget, Actual Expenditure and Forecast at Completion in SAR, and the Financial Condition. Totals add verified SAR figures only.", "SCR-049"),
        new(PublishedFinancialSnapshot, "WF-14", Version, ProjectionSemanticState.PublishedOfficial, Both, [DashboardWidgetType.MetricCard], PermissionCatalogue.FinancialView, true,
            ProjectionEligibility.Delivering, "The latest Published Financial Snapshot of WF-14, in SAR, with its Financial Condition as published. Totals add verified SAR figures only.", "SCR-049"),
        new(KpiCondition, "WF-14", Version, ProjectionSemanticState.PublishedOfficial, Both, Categorical, PermissionCatalogue.KpiView, false,
            ProjectionEligibility.Delivering, "ACTIVE KPI assignments counted by the RAG condition of their latest published measurement; unlike KPI values are never combined.", "SCR-050"),
        new(DefinitionBacklog, "FG-01", Version, ProjectionSemanticState.CurrentLive, [DashboardContextKind.Portfolio],
            [DashboardWidgetType.MetricCard, DashboardWidgetType.StatusDistribution, DashboardWidgetType.BarColumn], PermissionCatalogue.ConfigurationView, false,
            ProjectionEligibility.NoProject, "Dashboard versions on their way and in force, by lifecycle state: the configuration backlog of ADM-036 (DSH-001).", "ADM-036"),
    ];

    private static readonly Dictionary<string, ProjectionContract> ByCode = All.ToDictionary(p => p.Code, StringComparer.Ordinal);

    public static ProjectionContract? Find(string code) => ByCode.GetValueOrDefault(code);
}
