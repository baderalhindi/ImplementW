using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.FollowUp;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.Insights;

/// <summary>Narrows the projects as the one filter bar does. Filters narrow a scope; they never widen it.</summary>
public sealed record PortfolioInsightFilters(
    Guid? DepartmentId = null, Guid? ExternalEntityId = null, Guid? ClassificationItemId = null, Guid? RegionItemId = null, Guid? CityItemId = null, ProjectLifecycleState? Status = null,
    Guid? PriorityItemId = null, Guid? StrategicPortfolioItemId = null, Guid? StrategicInitiativeItemId = null, int? Period = null);

/// <summary>RPMO's six headline cards (the 5 Nov 2026 review §3 row 1). A value is null when the caller may not see amounts; an average is null when nothing is measured.</summary>
public sealed record PortfolioHeadline(
    int RegisteredProjects, int InProgressProjects, string? TotalValueSar, int ValueEntered, decimal? AveragePlannedPercent, decimal? AverageActualPercent, int Measured, decimal? GapPoints,
    OnPlanStatus? OnPlan, int NeedingFollowUp, int OverdueActions);

/// <summary>A row of a breakdown: by portfolio, initiative or sector. A total row is calculated from the projects, never from the rows' averages.</summary>
public sealed record PortfolioBreakdownRow(
    Guid? ItemId, Guid? ParentItemId, int Projects, string? ValueSar, int ValueEntered, int Measured, decimal? AveragePlannedPercent, decimal? AverageActualPercent, decimal? GapPoints, OnPlanStatus? OnPlan,
    int NeedingFollowUp, decimal? AveragePlannedSpendPercent = null, decimal? AverageActualSpendPercent = null, int SpendMeasured = 0, IReadOnlyList<PortfolioSplit>? PortfolioSplit = null);

public sealed record PortfolioSplit(Guid? PortfolioItemId, int Projects);

public sealed record PortfolioBreakdown(IReadOnlyList<PortfolioBreakdownRow> Rows, PortfolioBreakdownRow Total);

public sealed record PortfolioStageCount(ProjectLifecycleState Status, int Projects, decimal Percent);

public sealed record PortfolioOnPlan(int OnTrack, int Late, int Stalled, int NotMeasured);

/// <summary>Each project counted once, under its first reason in the Follow-up list's order.</summary>
public sealed record PortfolioFollowUpReasons(int Stalled, int Suspended, int Late, int OverdueAction, int OpenObstacle, int Total);

public sealed record PortfolioTopProject(
    Guid ProjectId, string? FormalProjectId, string? LegacyReference, NarrativeText Title, Guid? PortfolioItemId, FollowUpReason Reason, NarrativeText? CurrentObstacle, NarrativeText? NextAction,
    string? NextActionOwner, DateOnly? NextActionDueDate);

/// <summary>
/// What the Excel's dashboard answers (the 5 Nov 2026 review §3). A section is null when the caller holds no permission for it: permissions decide, never a role code. Averages are simple
/// averages of project percentages. A project without both percentages is unmeasured, never zero.
/// </summary>
public sealed record PortfolioInsights(
    PortfolioHeadline? Headline, PortfolioBreakdown? ByPortfolio, PortfolioBreakdown? ByInitiative, PortfolioBreakdown? BySector, IReadOnlyList<PortfolioStageCount>? Stages, PortfolioOnPlan? OnPlan,
    PortfolioFollowUpReasons? FollowUpReasons, IReadOnlyList<PortfolioTopProject>? TopFollowUp);

public enum PortfolioCardDimension
{
    Portfolio = 1,
    Initiative = 2,
    Sector = 3,
}

/// <summary>A project of a printable group card.</summary>
public sealed record PortfolioCardProject(
    Guid ProjectId, string? FormalProjectId, string? LegacyReference, NarrativeText Title, ProjectLifecycleState Status, Guid DepartmentId, Guid? ExternalEntityId, string? ValueSar,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, decimal? PlannedPercent, decimal? ActualPercent, decimal? GapPoints, OnPlanStatus? OnPlan, NarrativeText? CurrentObstacle, NarrativeText? NextAction,
    string? NextActionOwner, DateOnly? NextActionDueDate);

/// <summary>A printable portfolio, initiative or sector card (§3 row 11): a summary, then every project.</summary>
public sealed record PortfolioCard(PortfolioCardDimension Dimension, Guid ItemId, PortfolioBreakdownRow Summary, IReadOnlyList<PortfolioCardProject> Projects);

public interface IPortfolioInsightsService
{
    public Task<PortfolioInsights> GetAsync(Guid callerId, PortfolioInsightFilters filters, CancellationToken cancellationToken);

    public Task<AdministrationResult<PortfolioCard>> GetCardAsync(Guid callerId, PortfolioCardDimension dimension, Guid itemId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IPortfolioInsightsService"/>
internal sealed class PortfolioInsightsService(IProjectRepository repository, IAuthorizationEngine engine, ProjectIndicatorReader indicators, OnPlanPolicy onPlanPolicy, TimeProvider timeProvider) : IPortfolioInsightsService
{
    private const int TopCount = 10;

    /// <summary>What counts on these figures: a project past registration. A draft or a request under review is not yet the register's.</summary>
    private static readonly ProjectLifecycleState[] Registering =
    [
        ProjectLifecycleState.Draft, ProjectLifecycleState.Submitted, ProjectLifecycleState.UnderReview, ProjectLifecycleState.Returned,
    ];

    public async Task<PortfolioInsights> GetAsync(Guid callerId, PortfolioInsightFilters filters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filters);
        bool amounts = !(await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.FinancialViewSensitive, cancellationToken).ConfigureAwait(false)).IsEmpty;
        OnPlanThresholds thresholds = await onPlanPolicy.ThresholdsAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        async Task<IReadOnlyList<Row>?> Reach(string permission)
        {
            RecordScope scope = await engine.GetRecordScopeAsync(callerId, permission, cancellationToken).ConfigureAwait(false);
            return scope.IsEmpty ? null : await RowsAsync(scope, filters, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<Row>? summary = await Reach(PermissionCatalogue.PortfolioSummaryView).ConfigureAwait(false);
        IReadOnlyList<Row>? breakdown = await Reach(PermissionCatalogue.PortfolioBreakdownView).ConfigureAwait(false);
        IReadOnlyList<Row>? status = await Reach(PermissionCatalogue.PortfolioStatusView).ConfigureAwait(false);
        IReadOnlyList<Row>? followUp = await Reach(PermissionCatalogue.PortfolioFollowUpView).ConfigureAwait(false);

        return new PortfolioInsights(
            summary is null ? null : Headline(summary, amounts, thresholds),
            summary is null ? null : Breakdown(summary, r => r.Project.StrategicPortfolioItemId, _ => null, amounts, thresholds, sector: false),
            breakdown is null ? null : Breakdown([.. breakdown.Where(r => r.Project.StrategicInitiativeItemId is not null)], r => r.Project.StrategicInitiativeItemId, r => r.Project.StrategicPortfolioItemId, amounts, thresholds, sector: false),
            breakdown is null ? null : Breakdown(breakdown, r => r.Project.ClassificationItemId, _ => null, amounts, thresholds, sector: true),
            status is null ? null : Stages(status),
            status is null ? null : new PortfolioOnPlan(
                status.Count(r => r.Indicators.OnPlan == OnPlanStatus.OnTrack), status.Count(r => r.Indicators.OnPlan == OnPlanStatus.Late),
                status.Count(r => r.Indicators.OnPlan == OnPlanStatus.Stalled), status.Count(r => r.Indicators.OnPlan is null)),
            followUp is null ? null : Reasons(followUp),
            followUp is null ? null : [.. followUp.Where(r => r.Indicators.FollowUpReasons.Count > 0)
                .OrderBy(r => r.Indicators.FollowUpReasons[0])
                .ThenBy(r => r.Project.NextActionDueDate ?? DateOnly.MaxValue)
                .ThenBy(r => r.Project.FormalProjectId ?? r.Project.LegacyReference, StringComparer.Ordinal)
                .Take(TopCount)
                .Select(r => new PortfolioTopProject(
                    r.Project.Id, r.Project.FormalProjectId, r.Project.LegacyReference, r.Project.Title, r.Project.StrategicPortfolioItemId, r.Indicators.FollowUpReasons[0], r.Project.CurrentObstacle,
                    r.Project.NextAction, r.Project.NextActionOwner, r.Project.NextActionDueDate))]);
    }

    public async Task<AdministrationResult<PortfolioCard>> GetCardAsync(Guid callerId, PortfolioCardDimension dimension, Guid itemId, CancellationToken cancellationToken)
    {
        RecordScope scope = await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.PortfolioBreakdownView, cancellationToken).ConfigureAwait(false);
        if (scope.IsEmpty)
        {
            return AdministrationError.Forbidden;
        }

        bool amounts = !(await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.FinancialViewSensitive, cancellationToken).ConfigureAwait(false)).IsEmpty;
        OnPlanThresholds thresholds = await onPlanPolicy.ThresholdsAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        Func<ProjectEntity, Guid?> key = dimension switch
        {
            PortfolioCardDimension.Portfolio => p => p.StrategicPortfolioItemId,
            PortfolioCardDimension.Initiative => p => p.StrategicInitiativeItemId,
            PortfolioCardDimension.Sector => p => p.ClassificationItemId,
            _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown card dimension."),
        };
        List<Row> rows = [.. (await RowsAsync(scope, new PortfolioInsightFilters(), cancellationToken).ConfigureAwait(false)).Where(r => key(r.Project) == itemId)];
        return rows.Count == 0 ? AdministrationError.NotFound : new PortfolioCard(dimension, itemId, RowOf(itemId, null, rows, amounts, thresholds, sector: dimension == PortfolioCardDimension.Sector), [.. rows
            .OrderBy(r => r.Project.LegacyReference ?? r.Project.FormalProjectId, StringComparer.Ordinal)
            .Select(r => new PortfolioCardProject(
                r.Project.Id, r.Project.FormalProjectId, r.Project.LegacyReference, r.Project.Title, r.Project.LifecycleState, r.Project.DepartmentId, r.Project.ExternalEntityId,
                amounts ? Sar(r.Project.RegistrationBudgetSar) : null, r.Project.PlannedStartDate, r.Project.PlannedEndDate, r.Project.DeclaredPlannedPercent, r.Project.DeclaredActualPercent,
                r.Indicators.GapPoints, r.Indicators.OnPlan, r.Project.CurrentObstacle, r.Project.NextAction, r.Project.NextActionOwner, r.Project.NextActionDueDate))]);
    }

    private async Task<IReadOnlyList<Row>> RowsAsync(RecordScope scope, PortfolioInsightFilters filters, CancellationToken cancellationToken)
    {
        List<ProjectEntity> reached = [.. (await repository.ListReachedAsync(scope, cancellationToken).ConfigureAwait(false)).Where(p => !Registering.Contains(p.LifecycleState) && Matches(p, filters))];
        Func<ProjectEntity, ProjectIndicators> indicatorsOf = await indicators.ForAsync(reached, cancellationToken).ConfigureAwait(false);
        return [.. reached.Select(p => new Row(p, indicatorsOf(p)))];
    }

    private static bool Matches(ProjectEntity p, PortfolioInsightFilters f) =>
        (f.DepartmentId is not { } department || p.DepartmentId == department)
        && (f.ExternalEntityId is not { } entity || p.ExternalEntityId == entity)
        && (f.ClassificationItemId is not { } sector || p.ClassificationItemId == sector)
        && (f.RegionItemId is not { } region || p.RegionItemId == region)
        && (f.CityItemId is not { } city || p.CityItemId == city)
        && (f.Status is not { } status || p.LifecycleState == status)
        && (f.PriorityItemId is not { } priority || p.PriorityItemId == priority)
        && (f.StrategicPortfolioItemId is not { } portfolio || p.StrategicPortfolioItemId == portfolio)
        && (f.StrategicInitiativeItemId is not { } initiative || p.StrategicInitiativeItemId == initiative)
        && (f.Period is not { } year || TouchesYear(p, year));

    private static bool TouchesYear(ProjectEntity p, int year)
    {
        (DateOnly? start, DateOnly? end) = (p.PlannedStartDate ?? p.PlannedEndDate, p.PlannedEndDate ?? p.PlannedStartDate);
        return start is { } s && end is { } e && s.Year <= year && e.Year >= year;
    }

    private static PortfolioHeadline Headline(IReadOnlyList<Row> rows, bool amounts, OnPlanThresholds thresholds)
    {
        PortfolioBreakdownRow all = RowOf(null, null, rows, amounts, thresholds, sector: false);
        return new PortfolioHeadline(
            rows.Count, rows.Count(r => r.Project.LifecycleState == ProjectLifecycleState.Active), all.ValueSar, all.ValueEntered, all.AveragePlannedPercent, all.AverageActualPercent, all.Measured, all.GapPoints,
            all.OnPlan, all.NeedingFollowUp, rows.Count(r => r.Indicators.ActionDue == ActionDueStatus.Overdue));
    }

    private static PortfolioBreakdown Breakdown(
        IReadOnlyList<Row> rows, Func<Row, Guid?> key, Func<Row, Guid?> parent, bool amounts, OnPlanThresholds thresholds, bool sector) =>
        new(
            [.. rows.GroupBy(key).Select(g => RowOf(g.Key, parent(g.First()), [.. g], amounts, thresholds, sector)).OrderByDescending(r => r.Projects).ThenBy(r => r.ItemId)],
            RowOf(null, null, rows, amounts, thresholds, sector));

    private static PortfolioBreakdownRow RowOf(Guid? item, Guid? parentItem, IReadOnlyList<Row> rows, bool amounts, OnPlanThresholds thresholds, bool sector)
    {
        List<Row> measured = [.. rows.Where(r => r.Indicators.GapPoints is not null)];
        decimal? planned = Average(measured.Select(r => r.Project.DeclaredPlannedPercent));
        decimal? actual = Average(measured.Select(r => r.Project.DeclaredActualPercent));
        decimal? gap = planned is { } p && actual is { } a ? decimal.Round(a - p, 2) : null;
        List<Row> withSpend = [.. rows.Where(r => r.Project.DeclaredPlannedSpendPercent is not null && r.Project.DeclaredActualSpendPercent is not null)];
        return new PortfolioBreakdownRow(
            item, parentItem, rows.Count, amounts ? Sar(rows.Sum(r => r.Project.RegistrationBudgetSar?.Amount ?? 0m)) : null, rows.Count(r => r.Project.RegistrationBudgetSar is not null), measured.Count,
            Round(planned), Round(actual), gap, LabelOf(gap, thresholds), rows.Count(r => r.Indicators.FollowUpReasons.Count > 0),
            sector ? Round(Average(withSpend.Select(r => r.Project.DeclaredPlannedSpendPercent))) : null,
            sector ? Round(Average(withSpend.Select(r => r.Project.DeclaredActualSpendPercent))) : null,
            sector ? withSpend.Count : 0,
            sector ? [.. rows.GroupBy(r => r.Project.StrategicPortfolioItemId).Select(g => new PortfolioSplit(g.Key, g.Count())).OrderByDescending(s => s.Projects)] : null);
    }

    private static OnPlanStatus? LabelOf(decimal? gap, OnPlanThresholds thresholds) =>
        gap is not { } points ? null
        : points <= -thresholds.StalledPoints ? OnPlanStatus.Stalled
        : points <= -thresholds.LatePoints ? OnPlanStatus.Late
        : OnPlanStatus.OnTrack;

    private static IReadOnlyList<PortfolioStageCount> Stages(IReadOnlyList<Row> rows) =>
        [.. rows.GroupBy(r => r.Project.LifecycleState).OrderByDescending(g => g.Count()).Select(g => new PortfolioStageCount(g.Key, g.Count(), decimal.Round(g.Count() * 100m / rows.Count, 1)))];

    private static PortfolioFollowUpReasons Reasons(IReadOnlyList<Row> rows)
    {
        List<FollowUpReason> first = [.. rows.Where(r => r.Indicators.FollowUpReasons.Count > 0).Select(r => r.Indicators.FollowUpReasons[0])];
        int Count(FollowUpReason reason) => first.Count(r => r == reason);
        return new PortfolioFollowUpReasons(Count(FollowUpReason.Stalled), Count(FollowUpReason.Suspended), Count(FollowUpReason.Late), Count(FollowUpReason.ActionOverdue), Count(FollowUpReason.OpenObstacle), first.Count);
    }

    private static decimal? Average(IEnumerable<decimal?> values)
    {
        List<decimal> present = [.. values.OfType<decimal>()];
        return present.Count == 0 ? null : present.Sum() / present.Count;
    }

    private static decimal? Round(decimal? value) => value is { } v ? decimal.Round(v, 2) : null;

    private static string Sar(Money? amount) => amount is { } money ? Sar(money.Amount) : Sar(0m);

    private static string Sar(decimal amount) => amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record Row(ProjectEntity Project, ProjectIndicators Indicators);
}
