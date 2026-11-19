using System.Globalization;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// PROJECT.PROJECT_PROFILE: the project record's own registered attributes as WF-01 holds them (CURRENT/LIVE), read from <see cref="ProjectFacts"/> and never
/// computed (the 18 Nov 2026 designer fields). A value not set is absent (MISSING), never 0 or false. The registration budget is withheld (masked) from a caller
/// who holds no <c>FINANCIAL_VIEW_SENSITIVE</c> scope, the rule the portfolio insights' amounts use.
/// </summary>
internal sealed class ProjectProfileSource(IAuthorizationEngine engine) : IDashboardProjectionSource
{
    public const string RegistrationBudget = "REGISTRATION_BUDGET";

    private const string Boolean = "BOOLEAN";
    private const string Code = "CODE";
    private const string Date = "DATE";
    private const string DateTime = "DATE_TIME";

    /// <summary>The projection's fields, in the register's order.</summary>
    public static IReadOnlyList<ProjectionField> Fields { get; } =
    [
        ProjectionField.Figure(RegistrationBudget, ProjectionValueType.Sar, isSensitive: true),
        ProjectionField.Figure("DECLARED_ACTUAL_PERCENT", ProjectionValueType.Percent),
        ProjectionField.Figure("DECLARED_PLANNED_PERCENT", ProjectionValueType.Percent),
        ProjectionField.Figure("DECLARED_ACTUAL_SPEND_PERCENT", ProjectionValueType.Percent),
        ProjectionField.Figure("DECLARED_PLANNED_SPEND_PERCENT", ProjectionValueType.Percent),
        ProjectionField.Figure("CURRENT_STAGE_PERCENT", ProjectionValueType.Percent),
        ProjectionField.Figure("FUNDING_SOURCE", ProjectionValueType.Code, values: ProjectionReadings.Names<FundingSource>()),
        ProjectionField.Figure("PARTICIPATION_MODE", ProjectionValueType.Code, values: ProjectionReadings.Names<ParticipationMode>()),
        ProjectionField.Figure("PROGRESS_METHOD", ProjectionValueType.Code, values: ProjectionReadings.Names<ProjectProgressMethod>()),
        ProjectionField.Figure("IS_STRATEGIC", ProjectionValueType.Boolean),
        ProjectionField.Figure("ENTITY_UPDATE_COMPLIANT", ProjectionValueType.Boolean),
        ProjectionField.Figure("PLANNED_START_DATE", ProjectionValueType.Date),
        ProjectionField.Figure("PLANNED_END_DATE", ProjectionValueType.Date),
        ProjectionField.Figure("NEXT_ACTION_DUE_DATE", ProjectionValueType.Date),
        ProjectionField.Figure("ACTIVATED_AT", ProjectionValueType.DateTime),
        ProjectionField.Figure("CLOSED_AT", ProjectionValueType.DateTime),
    ];

    public string ProjectionCode => DashboardProjections.ProjectProfile;

    /// <summary>One project's attributes on the Project Dashboard; over a population the attributes are not combined (a report row presents them).</summary>
    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken) =>
        request.Read(await ObserveAsync(request, cancellationToken).ConfigureAwait(false), _ => ProjectionReadings.State(null));

    public async Task<IReadOnlyList<Observation>> ObserveAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool amounts = request.Projects.Count > 0
            && !(await engine.GetRecordScopeAsync(request.CallerId, PermissionCatalogue.FinancialViewSensitive, cancellationToken).ConfigureAwait(false)).IsEmpty;
        return [.. request.Projects.Select(p => Observe(p, amounts, request.Now))];
    }

    /// <summary>A project's attributes as figures; the budget masked unless <paramref name="amounts"/>.</summary>
    internal static Observation Observe(ProjectFacts p, bool amounts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new Observation(
            p.Id, ObservationKind.Current, now,
            ProjectionReadings.State(
                null,
                ProjectionReadings.Sar(RegistrationBudget, p.RegistrationBudgetSar, masked: !amounts),
                ProjectionReadings.Percent("DECLARED_ACTUAL_PERCENT", p.DeclaredActualPercent),
                ProjectionReadings.Percent("DECLARED_PLANNED_PERCENT", p.DeclaredPlannedPercent),
                ProjectionReadings.Percent("DECLARED_ACTUAL_SPEND_PERCENT", p.DeclaredActualSpendPercent),
                ProjectionReadings.Percent("DECLARED_PLANNED_SPEND_PERCENT", p.DeclaredPlannedSpendPercent),
                ProjectionReadings.Percent("CURRENT_STAGE_PERCENT", p.CurrentStagePercent),
                new WidgetFigure("FUNDING_SOURCE", p.FundingSource is { } funding ? ProjectionReadings.Name(funding) : null, Code, false),
                new WidgetFigure("PARTICIPATION_MODE", ProjectionReadings.Name(p.ParticipationMode), Code, false),
                new WidgetFigure("PROGRESS_METHOD", p.ProgressMethod is { } method ? ProjectionReadings.Name(method) : null, Code, false),
                new WidgetFigure("IS_STRATEGIC", BooleanOf(p.IsStrategic), Boolean, false),
                new WidgetFigure("ENTITY_UPDATE_COMPLIANT", BooleanOf(p.EntityUpdateCompliant), Boolean, false),
                new WidgetFigure("PLANNED_START_DATE", DateOf(p.PlannedStartDate), Date, false),
                new WidgetFigure("PLANNED_END_DATE", DateOf(p.PlannedEndDate), Date, false),
                new WidgetFigure("NEXT_ACTION_DUE_DATE", DateOf(p.NextActionDueDate), Date, false),
                new WidgetFigure("ACTIVATED_AT", MomentOf(p.ActivatedAt), DateTime, false),
                new WidgetFigure("CLOSED_AT", MomentOf(p.ClosedAt), DateTime, false)),
            amounts || p.RegistrationBudgetSar is null ? null : [RegistrationBudget]);
    }

    private static string? BooleanOf(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => null,
    };

    private static string? DateOf(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? MomentOf(DateTimeOffset? moment) => moment?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
