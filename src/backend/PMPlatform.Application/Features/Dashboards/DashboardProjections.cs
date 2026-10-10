using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Projections;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Dashboards.Projections;
using PMPlatform.Domain.Dashboards;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.Progress;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Schedule;

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
/// revision of this register (FG-01 §13.5, BR-DSH-035). <see cref="Fields"/> are what a report may present of it, row by row, and
/// <see cref="Grain"/> whether those rows are projects or the source's snapshots of one (FG-02 §4.1; TASK-071); a projection that is not
/// about projects has no grain and no field.
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
    string? DrillTargetScreenId,
    ProjectionGrain? Grain,
    IReadOnlyList<ProjectionField> Fields)
{
    /// <summary>The code as one word, as configuration names it (REPORT_RULES <c>sourceEntityCode</c>): the dot becomes an underscore.</summary>
    public string EntityCode { get; } = Code.Replace('.', '_');

    public bool Supports(DashboardContextKind context) => Contexts.Contains(context);

    public ProjectionField? Field(string code) => Fields.FirstOrDefault(f => f.Code == code);

    public bool IsEligible(ProjectLifecycleState state) => Eligibility switch
    {
        ProjectionEligibility.AnyProject => true,
        ProjectionEligibility.Delivering => state is ProjectLifecycleState.Active or ProjectLifecycleState.Suspended or ProjectLifecycleState.Completed or ProjectLifecycleState.Closed,
        ProjectionEligibility.Reporting => state == ProjectLifecycleState.Active,
        ProjectionEligibility.NoProject => false,
        _ => throw new ArgumentOutOfRangeException(nameof(state), Eligibility, "Unknown eligibility."),
    };

    public DashboardProjectionDetail ToDetail() =>
        new(Code, SourceDomain, Version, SemanticState, Contexts, WidgetTypes, PermissionCode, IsSensitive, BusinessMeaning, DrillTargetScreenId,
            EntityCode, Grain, [.. Fields.Select(f => f.ToDescriptor())]);

    public ProjectionDescriptor ToDescriptor() =>
        new(Code, EntityCode, SourceDomain, Version, SemanticState, PermissionCode, IsSensitive,
            Grain ?? throw new InvalidOperationException($"{Code} is not about projects and has no rows."), BusinessMeaning, [.. Fields.Select(f => f.ToDescriptor())]);
}

/// <summary>
/// The projection register (FG-01 §4.2, Projection Registry of §24): every source a widget or a report may bind to, and nothing else —
/// ADM-036 and ADM-037 bind only these codes and fields, so no configuration can name a table, a query or a formula (BR-DSH-029,
/// BR-DSH-030, BR-RPT-046). Each is read through its owning module's contracts (ADR-003 §8.2 edges 29, 47 and 48 to 52), never its
/// tables (CFA-DSH-006). FG-02 reads the same register (edge 30, TASK-071).
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
    public const string OpenTasks = "PROJECT_TASK.OPEN_TASKS";
    public const string OpenAchievementClaims = "MILESTONE.OPEN_ACHIEVEMENT_CLAIMS";
    public const string OpenConcerns = "MANAGEMENT_CONCERN.OPEN_CONCERNS";
    public const string ChangePosition = "CHANGE_REQUEST.CHANGE_POSITION";
    public const string OpenSuspensionRequests = "SUSPENSION.OPEN_REQUESTS";

    private const string Version = "1";

    private static readonly DashboardContextKind[] Both = [DashboardContextKind.Project, DashboardContextKind.Portfolio];

    private static readonly DashboardWidgetType[] Categorical =
        [DashboardWidgetType.MetricCard, DashboardWidgetType.StatusDistribution, DashboardWidgetType.BarColumn, DashboardWidgetType.DonutPie];

    private static readonly DashboardWidgetType[] Counts = [DashboardWidgetType.MetricCard];

    private static readonly IReadOnlyList<string> Lifecycle = ProjectionReadings.Names<ProjectLifecycleState>();

    private static readonly IReadOnlyList<string> Health = ProjectionReadings.Names<HealthStatus>();

    private static readonly ProjectionField[] FinancialFields =
    [
        ProjectionField.State("FINANCIAL_CONDITION", ProjectionReadings.Names<FinancialStatus>()),
        ProjectionField.Figure(FinancialReadings.ApprovedBudget, ProjectionValueType.Sar, isSensitive: true),
        ProjectionField.Figure(FinancialReadings.ActualExpenditureToDate, ProjectionValueType.Sar, isSensitive: true),
        ProjectionField.Figure(FinancialReadings.ForecastAtCompletion, ProjectionValueType.Sar, isSensitive: true),
    ];

    public static IReadOnlyList<ProjectionContract> All { get; } =
    [
        new(ProjectLifecycleState, "WF-01", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.ProjectView, false,
            ProjectionEligibility.AnyProject, "The project's lifecycle state as WF-01 holds it; over a population, the count of authorised projects in each state, each project once.", "SCR-025",
            ProjectionGrain.Project, [ProjectionField.State("LIFECYCLE_STATE", Lifecycle)]),
        new(ProjectHealthStatus, "WF-02", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.ProgressView, false,
            ProjectionEligibility.Delivering, "Overall Project Health as WF-02 last computed it, with the live actual and planned percentages; never recomputed here (ICD-03).", "SCR-027",
            ProjectionGrain.Project, [ProjectionField.State("OVERALL_HEALTH", Health), ProjectionField.Figure("ACTUAL_PERCENT", ProjectionValueType.Percent), ProjectionField.Figure("PLANNED_PERCENT", ProjectionValueType.Percent)]),
        new(PublishedProgressSnapshot, "WF-02", Version, ProjectionSemanticState.PublishedOfficial, Both, [.. Categorical, DashboardWidgetType.ProgressIndicator],
            PermissionCatalogue.ProgressView, false, ProjectionEligibility.Delivering,
            "The latest Published Progress Snapshot: the official Overall Project Health and progress percentages. Stale while a later reporting period is past due unpublished.", "SCR-040",
            ProjectionGrain.Project,
            [
                ProjectionField.State("OVERALL_HEALTH", Health), ProjectionField.Figure("ACTUAL_PERCENT", ProjectionValueType.Percent),
                ProjectionField.Figure("PLANNED_PERCENT", ProjectionValueType.Percent), ProjectionField.Figure("ACTUAL_PERCENT_OVERRIDDEN", ProjectionValueType.Boolean),
            ]),
        new(PublishedProgressHistory, "WF-02", Version, ProjectionSemanticState.HistoricalSnapshot, [DashboardContextKind.Project], [DashboardWidgetType.LineTrend],
            PermissionCatalogue.ProgressView, false, ProjectionEligibility.Delivering,
            "The project's immutable Published Progress Snapshots, oldest first: WF-02's own history, never rebuilt from current records.", "SCR-040",
            ProjectionGrain.Snapshot,
            [
                ProjectionField.Snapshot("PUBLISHED_AT", ProjectionValueType.DateTime, ProjectionFieldSource.SnapshotAsOf),
                ProjectionField.Snapshot("PERIOD_START", ProjectionValueType.Date, ProjectionFieldSource.SnapshotPeriodStart),
                ProjectionField.Snapshot("PERIOD_END", ProjectionValueType.Date, ProjectionFieldSource.SnapshotPeriodEnd),
                ProjectionField.Snapshot("OVERALL_HEALTH", ProjectionValueType.Code, ProjectionFieldSource.SnapshotState, Health),
                ProjectionField.Snapshot("ACTUAL_PERCENT", ProjectionValueType.Percent, ProjectionFieldSource.SnapshotFigure),
                ProjectionField.Snapshot("PLANNED_PERCENT", ProjectionValueType.Percent, ProjectionFieldSource.SnapshotFigure),
            ]),
        new(PublishedScheduleHealth, "WF-02", Version, ProjectionSemanticState.PublishedOfficial, Both, Categorical, PermissionCatalogue.ProgressView, false,
            ProjectionEligibility.Delivering, "WF-03's Schedule Health as WF-02 published it in the latest snapshot: the schedule status an entity sees on its own project (ADR-013).", "SCR-040",
            ProjectionGrain.Project, [ProjectionField.State("SCHEDULE_HEALTH", Health)]),
        new(ReportingCompleteness, "WF-02", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.ProgressView, false,
            ProjectionEligibility.Reporting, "Whether an ACTIVE project's progress reporting is up to date: OVERDUE while a reporting period is past its due date unpublished. Not a measure of delay.", "SCR-040",
            ProjectionGrain.Project,
            [
                ProjectionField.State("REPORTING_STATUS", [ReportingCompletenessSource.UpToDate, ReportingCompletenessSource.Overdue]),
                ProjectionField.Figure("OVERDUE_PERIODS", ProjectionValueType.Count),
            ]),
        new(ScheduleHealthStatus, "WF-03", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.ScheduleView, false,
            ProjectionEligibility.Delivering, "Schedule Health as WF-03 last computed it against the Approved Baseline, with the finish variance in days; never re-derived from tasks.", "SCR-028",
            ProjectionGrain.Project,
            [ProjectionField.State("SCHEDULE_HEALTH", ProjectionReadings.Names<ScheduleHealth>()), ProjectionField.Figure("FINISH_VARIANCE_DAYS", ProjectionValueType.Days)]),
        new(RiskExposure, "WF-06", Version, ProjectionSemanticState.CurrentLive, Both, Categorical, PermissionCatalogue.RiskView, false,
            ProjectionEligibility.Delivering, "Open risks counted by the rating their latest assessment recorded (WF-06), with those not yet assessed and those past review; never rescored.", "SCR-080",
            ProjectionGrain.Project,
            [
                ProjectionField.Figure("OPEN_RISKS", ProjectionValueType.Count), ProjectionField.Figure("NOT_ASSESSED", ProjectionValueType.Count),
                ProjectionField.Figure("REVIEW_OVERDUE", ProjectionValueType.Count),
            ]),
        new(FinancialPosition, "WF-14", Version, ProjectionSemanticState.CurrentLive, Both, [DashboardWidgetType.MetricCard], PermissionCatalogue.FinancialView, true,
            ProjectionEligibility.Delivering, "WF-14's live financial position: Approved Budget, Actual Expenditure and Forecast at Completion in SAR, and the Financial Condition. Totals add verified SAR figures only.", "SCR-049",
            ProjectionGrain.Project, FinancialFields),
        new(PublishedFinancialSnapshot, "WF-14", Version, ProjectionSemanticState.PublishedOfficial, Both, [DashboardWidgetType.MetricCard], PermissionCatalogue.FinancialView, true,
            ProjectionEligibility.Delivering, "The latest Published Financial Snapshot of WF-14, in SAR, with its Financial Condition as published. Totals add verified SAR figures only.", "SCR-049",
            ProjectionGrain.Project, FinancialFields),
        new(KpiCondition, "WF-14", Version, ProjectionSemanticState.PublishedOfficial, Both, Categorical, PermissionCatalogue.KpiView, false,
            ProjectionEligibility.Delivering, "ACTIVE KPI assignments counted by the RAG condition of their latest published measurement; unlike KPI values are never combined.", "SCR-050",
            ProjectionGrain.Project,
            [
                ProjectionField.Figure("ACTIVE_ASSIGNMENTS", ProjectionValueType.Count), ProjectionField.Figure(KpiConditionSource.NotPublished, ProjectionValueType.Count),
                .. ProjectionReadings.Names<KpiRagStatus>().Select(rag => ProjectionField.Bucket($"RAG_{rag}", rag)),
            ]),
        new(DefinitionBacklog, "FG-01", Version, ProjectionSemanticState.CurrentLive, [DashboardContextKind.Portfolio],
            [DashboardWidgetType.MetricCard, DashboardWidgetType.StatusDistribution, DashboardWidgetType.BarColumn], PermissionCatalogue.ConfigurationView, false,
            ProjectionEligibility.NoProject, "Dashboard versions on their way and in force, by lifecycle state: the configuration backlog of ADM-036 (DSH-001).", "ADM-036",
            null, []),
        new(OpenTasks, "WF-04", Version, ProjectionSemanticState.CurrentLive, Both, Counts, PermissionCatalogue.TaskView, false,
            ProjectionEligibility.Delivering, "The project's tasks and subtasks neither COMPLETED nor CANCELLED, as WF-04 counts them; read live, never re-derived from the schedule.", "SCR-047",
            ProjectionGrain.Project, [ProjectionField.Figure("OPEN_TASKS", ProjectionValueType.Count)]),
        new(OpenAchievementClaims, "WF-05", Version, ProjectionSemanticState.CurrentLive, Both, Counts, PermissionCatalogue.MilestoneView, false,
            ProjectionEligibility.Delivering, "Milestone achievement claims DRAFT, SUBMITTED or RETURNED — neither accepted nor superseded — as WF-05 counts them; a claim is never an achievement.", "SCR-062",
            ProjectionGrain.Project, [ProjectionField.Figure("OPEN_ACHIEVEMENT_CLAIMS", ProjectionValueType.Count)]),
        new(OpenConcerns, "WF-07", Version, ProjectionSemanticState.CurrentLive, Both, Counts, PermissionCatalogue.ConcernView, false,
            ProjectionEligibility.Delivering, "Issues and challenges neither RESOLVED nor CLOSED, as WF-07 counts them; severity and priority are WF-07's and are not combined here.", "SCR-083",
            ProjectionGrain.Project, [ProjectionField.Figure("OPEN_CONCERNS", ProjectionValueType.Count)]),
        new(ChangePosition, "WF-08", Version, ProjectionSemanticState.CurrentLive, Both, Counts, PermissionCatalogue.ChangeRequestView, false,
            ProjectionEligibility.Delivering, "Change requests not yet decided, approved and not started, and in implementation, as WF-08 counts them: approved is never implemented.", "SCR-105",
            ProjectionGrain.Project,
            [
                ProjectionField.Figure("UNDECIDED", ProjectionValueType.Count), ProjectionField.Figure("APPROVED_NOT_STARTED", ProjectionValueType.Count),
                ProjectionField.Figure("IN_IMPLEMENTATION", ProjectionValueType.Count),
            ]),
        new(OpenSuspensionRequests, "WF-09", Version, ProjectionSemanticState.CurrentLive, Both, Counts, PermissionCatalogue.SuspensionView, false,
            ProjectionEligibility.Delivering, "Suspension and resumption requests not yet REJECTED, WITHDRAWN or EFFECTED, as WF-09 counts them: a request is never an active suspension.", "SCR-108",
            ProjectionGrain.Project, [ProjectionField.Figure("OPEN_REQUESTS", ProjectionValueType.Count)]),
    ];

    /// <summary>The projections a report may read: those about projects.</summary>
    public static IReadOnlyList<ProjectionContract> Reportable { get; } = [.. All.Where(p => p.Grain is not null)];

    private static readonly Dictionary<string, ProjectionContract> ByCode = All.ToDictionary(p => p.Code, StringComparer.Ordinal);

    private static readonly Dictionary<string, ProjectionContract> ByEntityCode = All.ToDictionary(p => p.EntityCode, StringComparer.Ordinal);

    public static ProjectionContract? Find(string code) => ByCode.GetValueOrDefault(code);

    /// <summary>The projection configuration names <paramref name="entityCode"/> (its code without the dot).</summary>
    public static ProjectionContract? FindByEntityCode(string entityCode) => ByEntityCode.GetValueOrDefault(entityCode);
}
