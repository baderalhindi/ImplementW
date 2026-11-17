using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.FollowUp;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Insights;

/// <summary>A visual as the designer sends it: a title in both languages, a type, one to three measures, a grouping and a place on the grid.</summary>
public sealed record PortfolioVisualInput(
    BilingualLabel Title, PortfolioVisualType VisualType, IReadOnlyList<PortfolioMeasure> Measures, PortfolioGroupBy GroupBy, short LayoutColumn, short LayoutRow, short LayoutWidth,
    short LayoutHeight);

/// <summary>The whole design, in reading order.</summary>
public sealed record PortfolioDesignInput(IReadOnlyList<PortfolioVisualInput> Visuals)
{
    public const int MaxVisuals = 40;
}

/// <summary>
/// One group of a visual: its key (an item, department or entity id, a status, stage or on-plan code; null for the projects with none, and for the one group of an
/// ungrouped visual), the stage's name when it is a stage, and one value per measure in the visual's order. A value is null when it cannot be calculated (an average
/// with nothing measured) or the caller may not see it (an amount). <see cref="Measured"/> and <see cref="ValueEntered"/> say how many projects the averages and the
/// value cover.
/// </summary>
public sealed record PortfolioVisualGroup(
    string? Key, BilingualLabel? Label, int Projects, int Measured, int ValueEntered, IReadOnlyList<decimal?> Values, OnPlanStatus? OnPlan);

/// <summary>A visual with its figures; <see cref="Groups"/> is null when the caller holds no permission for its grouping, and the dashboard leaves it out.</summary>
public sealed record PortfolioVisualResult(
    BilingualLabel Title, PortfolioVisualType VisualType, IReadOnlyList<PortfolioMeasure> Measures, PortfolioGroupBy GroupBy, short LayoutColumn, short LayoutRow, short LayoutWidth,
    short LayoutHeight, IReadOnlyList<PortfolioVisualGroup>? Groups);

/// <summary>The Portfolio dashboard's design with its figures. <see cref="IsStandard"/> is true while nobody has saved a design.</summary>
public sealed record PortfolioDesignView(bool IsStandard, DateTimeOffset? UpdatedAt, IReadOnlyList<PortfolioVisualResult> Visuals);

/// <summary>
/// The Portfolio dashboard's designer (17 Nov 2026): the Portfolio Manager places, sizes, adds and removes visuals for everyone, with no approval. A visual is a
/// fixed type, measures and a grouping, never an expression (BR-DSH-030); its figures are those of the insights, over the same projects
/// (<see cref="PortfolioProjectRows"/>), each grouping under the insights permission that shows that breakdown.
/// </summary>
public interface IPortfolioDesignService
{
    /// <summary>The saved design, or the standard one, with the caller's figures under the filters.</summary>
    public Task<PortfolioDesignView> GetAsync(Guid callerId, PortfolioInsightFilters filters, CancellationToken cancellationToken);

    /// <summary>A design not yet saved, with the caller's figures: what the designer shows while editing.</summary>
    public Task<AdministrationResult<PortfolioDesignView>> PreviewAsync(Guid callerId, PortfolioDesignInput design, PortfolioInsightFilters filters, CancellationToken cancellationToken);

    /// <summary>Replaces the design as a whole.</summary>
    public Task<AdministrationResult<PortfolioDesignView>> SaveAsync(Guid callerId, PortfolioDesignInput design, CancellationToken cancellationToken);

    /// <summary>Goes back to the standard design.</summary>
    public Task ResetAsync(CancellationToken cancellationToken);
}

/// <summary>The <c>project.portfolio_visual</c> table.</summary>
public interface IPortfolioVisualRepository
{
    public Task<IReadOnlyList<PortfolioVisual>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Deletes every visual and adds <paramref name="visuals"/>, in one save.</summary>
    public Task ReplaceAsync(IReadOnlyList<PortfolioVisual> visuals, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IPortfolioDesignService"/>
internal sealed class PortfolioDesignService(
    IPortfolioVisualRepository repository, PortfolioProjectRows projectRows, IAuthorizationEngine engine, OnPlanPolicy onPlanPolicy, IConfigurationResolver resolver,
    TimeProvider timeProvider) : IPortfolioDesignService
{
    public const int MaxTitleLength = 100;
    private const int Columns = 12;
    private const int MaxHeight = 12;

    /// <summary>The design before anyone changes it: the six headline cards and the two charts by portfolio, as the dashboard drew them.</summary>
    public static IReadOnlyList<PortfolioVisualInput> StandardDesign { get; } =
    [
        Card("المشاريع المسجلة", "Registered projects", 0, 0, [PortfolioMeasure.Projects, PortfolioMeasure.InProgress]),
        Card("إجمالي القيمة", "Total value", 4, 0, [PortfolioMeasure.ValueSar]),
        Card("متوسط المخطط", "Average planned", 8, 0, [PortfolioMeasure.AveragePlanned]),
        Card("متوسط الفعلي", "Average actual", 0, 2, [PortfolioMeasure.AverageActual]),
        Card("الفارق (نقاط)", "Gap (points)", 4, 2, [PortfolioMeasure.GapPoints]),
        Card("تحتاج متابعة", "Needing follow-up", 8, 2, [PortfolioMeasure.NeedingFollowUp, PortfolioMeasure.OverdueActions]),
        new(new BilingualLabel("المشاريع حسب المحفظة", "Projects by portfolio"), PortfolioVisualType.Bar, [PortfolioMeasure.Projects], PortfolioGroupBy.Portfolio, 0, 4, 6, 5),
        new(new BilingualLabel("المخطط والفعلي حسب المحفظة", "Planned and actual by portfolio"), PortfolioVisualType.Column,
            [PortfolioMeasure.AveragePlanned, PortfolioMeasure.AverageActual], PortfolioGroupBy.Portfolio, 6, 4, 6, 5),
    ];

    public async Task<PortfolioDesignView> GetAsync(Guid callerId, PortfolioInsightFilters filters, CancellationToken cancellationToken)
    {
        IReadOnlyList<PortfolioVisual> saved = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return saved.Count == 0
            ? await ViewAsync(callerId, StandardDesign, filters, isStandard: true, updatedAt: null, cancellationToken).ConfigureAwait(false)
            : await ViewAsync(callerId, [.. saved.Select(InputOf)], filters, isStandard: false, saved.Max(v => v.UpdatedAt), cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<PortfolioDesignView>> PreviewAsync(
        Guid callerId, PortfolioDesignInput design, PortfolioInsightFilters filters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(design);
        return Issue(design) is { } issue
            ? AdministrationError.Rule(ProjectErrorCodes.PortfolioDesignInvalid, issue)
            : await ViewAsync(callerId, design.Visuals, filters, isStandard: false, updatedAt: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<PortfolioDesignView>> SaveAsync(Guid callerId, PortfolioDesignInput design, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(design);
        if (Issue(design) is { } issue)
        {
            return AdministrationError.Rule(ProjectErrorCodes.PortfolioDesignInvalid, issue);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        await repository.ReplaceAsync(
            [.. design.Visuals.Select((v, index) => new PortfolioVisual
            {
                Id = Guid.CreateVersion7(now),
                SortOrder = (short)(index + 1),
                TitleAr = v.Title.Ar.Trim(),
                TitleEn = v.Title.En.Trim(),
                VisualType = v.VisualType,
                Measures = [.. v.Measures.Select(Code)],
                GroupBy = v.GroupBy,
                LayoutColumn = v.LayoutColumn,
                LayoutRow = v.LayoutRow,
                LayoutWidth = v.LayoutWidth,
                LayoutHeight = v.LayoutHeight,
                CreatedAt = now,
                CreatedBy = callerId,
                UpdatedAt = now,
                UpdatedBy = callerId,
            })],
            cancellationToken).ConfigureAwait(false);
        return await ViewAsync(callerId, design.Visuals, new PortfolioInsightFilters(), isStandard: false, now, cancellationToken).ConfigureAwait(false);
    }

    public Task ResetAsync(CancellationToken cancellationToken) => repository.ReplaceAsync([], cancellationToken);

    /// <summary>The permission that shows a grouping on the dashboard today: the headline and portfolios, the breakdowns, or stages and the on-plan indicator.</summary>
    internal static string PermissionOf(PortfolioGroupBy groupBy) => groupBy switch
    {
        PortfolioGroupBy.None or PortfolioGroupBy.Portfolio => PermissionCatalogue.PortfolioSummaryView,
        PortfolioGroupBy.Status or PortfolioGroupBy.Stage or PortfolioGroupBy.OnPlan => PermissionCatalogue.PortfolioStatusView,
        PortfolioGroupBy.Initiative or PortfolioGroupBy.Sector or PortfolioGroupBy.Department or PortfolioGroupBy.Entity or PortfolioGroupBy.Region
            or PortfolioGroupBy.City or PortfolioGroupBy.Priority => PermissionCatalogue.PortfolioBreakdownView,
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, "Unknown grouping."),
    };

    /// <summary>What is wrong with a design, or null. Visuals may not overlap; a card and a gauge show one figure, the others a figure per group.</summary>
    internal static FieldIssue? Issue(PortfolioDesignInput design)
    {
        if (design.Visuals is not { Count: > 0 and <= PortfolioDesignInput.MaxVisuals } visuals)
        {
            return new FieldIssue("visuals", design.Visuals is null || design.Visuals.Count == 0 ? FieldIssue.Required : FieldIssue.OutsideRange);
        }

        HashSet<(int Column, int Row)> taken = [];
        for (int index = 0; index < visuals.Count; index++)
        {
            PortfolioVisualInput v = visuals[index];
            string at = $"visuals[{index}]";
            if (v.Title is null || string.IsNullOrWhiteSpace(v.Title.Ar) || string.IsNullOrWhiteSpace(v.Title.En))
            {
                return new FieldIssue($"{at}.title", FieldIssue.Required);
            }

            if (v.Title.Ar.Trim().Length > MaxTitleLength || v.Title.En.Trim().Length > MaxTitleLength)
            {
                return new FieldIssue($"{at}.title", FieldIssue.OutsideRange);
            }

            if (!Enum.IsDefined(v.VisualType) || !Enum.IsDefined(v.GroupBy))
            {
                return new FieldIssue($"{at}.visualType", FieldIssue.NotAllowed);
            }

            if (v.Measures is not { Count: > 0 and <= 3 } measures || measures.Any(m => !Enum.IsDefined(m)))
            {
                return new FieldIssue($"{at}.measures", v.Measures is null || v.Measures.Count == 0 ? FieldIssue.Required : FieldIssue.OutsideRange);
            }

            if (measures.Distinct().Count() != measures.Count)
            {
                return new FieldIssue($"{at}.measures", FieldIssue.Duplicate);
            }

            if (ShapeIssue(v) is { } shape)
            {
                return new FieldIssue($"{at}.{shape}", FieldIssue.NotAllowed);
            }

            if (v.LayoutColumn < 0 || v.LayoutWidth < 1 || v.LayoutColumn + v.LayoutWidth > Columns || v.LayoutRow < 0 || v.LayoutHeight is < 1 or > MaxHeight)
            {
                return new FieldIssue($"{at}.layout", FieldIssue.OutsideRange);
            }

            for (int column = v.LayoutColumn; column < v.LayoutColumn + v.LayoutWidth; column++)
            {
                for (int row = v.LayoutRow; row < v.LayoutRow + v.LayoutHeight; row++)
                {
                    if (!taken.Add((column, row)))
                    {
                        return new FieldIssue($"{at}.layout", FieldIssue.InUse);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The field a visual's type does not allow, or null: a card and a gauge are not grouped, a chart is, a table either; a bar, pie, donut and gauge draw one
    /// measure, a card two (the second under the first), columns and a table three; a gauge draws a percentage, a pie and a donut a total
    /// that adds up (a share of an average means nothing).
    /// </summary>
    private static string? ShapeIssue(PortfolioVisualInput v)
    {
        bool single = v.VisualType is PortfolioVisualType.Card or PortfolioVisualType.Gauge;
        if (single != (v.GroupBy == PortfolioGroupBy.None) && v.VisualType != PortfolioVisualType.Table)
        {
            return "groupBy";
        }

        int maxMeasures = v.VisualType switch
        {
            PortfolioVisualType.Bar or PortfolioVisualType.Pie or PortfolioVisualType.Donut or PortfolioVisualType.Gauge => 1,
            PortfolioVisualType.Card => 2,
            PortfolioVisualType.Column or PortfolioVisualType.Table => 3,
            _ => 0,
        };
        bool percentOnly = v.VisualType == PortfolioVisualType.Gauge;
        bool additiveOnly = v.VisualType is PortfolioVisualType.Pie or PortfolioVisualType.Donut;
        return v.Measures.Count > maxMeasures
               || (percentOnly && v.Measures[0] is not (PortfolioMeasure.AveragePlanned or PortfolioMeasure.AverageActual))
               || (additiveOnly && v.Measures[0] is PortfolioMeasure.AveragePlanned or PortfolioMeasure.AverageActual or PortfolioMeasure.GapPoints)
            ? "measures"
            : null;
    }

    private async Task<PortfolioDesignView> ViewAsync(
        Guid callerId, IReadOnlyList<PortfolioVisualInput> visuals, PortfolioInsightFilters filters, bool isStandard, DateTimeOffset? updatedAt, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool amounts = !(await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.FinancialViewSensitive, cancellationToken).ConfigureAwait(false)).IsEmpty;
        OnPlanThresholds thresholds = await onPlanPolicy.ThresholdsAsync(now, cancellationToken).ConfigureAwait(false);
        ProjectStageSet? stages = visuals.Any(v => v.GroupBy == PortfolioGroupBy.Stage)
            ? await resolver.ResolveStagesAsync(now, cancellationToken).ConfigureAwait(false)
            : null;

        // Each permission's projects are read once, however many visuals use it.
        Dictionary<string, IReadOnlyList<PortfolioProjectRow>?> rowsByPermission = new(StringComparer.Ordinal);
        foreach (string permission in visuals.Select(v => PermissionOf(v.GroupBy)).Distinct(StringComparer.Ordinal))
        {
            RecordScope scope = await engine.GetRecordScopeAsync(callerId, permission, cancellationToken).ConfigureAwait(false);
            rowsByPermission[permission] = scope.IsEmpty ? null : await projectRows.ListAsync(scope, filters, cancellationToken).ConfigureAwait(false);
        }

        return new PortfolioDesignView(isStandard, updatedAt, [.. visuals.Select(v =>
        {
            IReadOnlyList<PortfolioProjectRow>? rows = rowsByPermission[PermissionOf(v.GroupBy)];
            return new PortfolioVisualResult(
                v.Title, v.VisualType, v.Measures, v.GroupBy, v.LayoutColumn, v.LayoutRow, v.LayoutWidth, v.LayoutHeight,
                rows is null ? null : Groups(v, rows, stages, amounts, thresholds));
        })]);
    }

    private static IReadOnlyList<PortfolioVisualGroup> Groups(
        PortfolioVisualInput visual, IReadOnlyList<PortfolioProjectRow> rows, ProjectStageSet? stages, bool amounts, OnPlanThresholds thresholds)
    {
        PortfolioVisualGroup GroupOf(string? key, BilingualLabel? label, IReadOnlyList<PortfolioProjectRow> members)
        {
            PortfolioBreakdownRow figures = PortfolioInsightsService.RowOf(null, null, members, amounts, thresholds, sector: false);
            return new PortfolioVisualGroup(
                key, label, figures.Projects, figures.Measured, figures.ValueEntered, [.. visual.Measures.Select(m => ValueOf(m, figures, members))], figures.OnPlan);
        }

        if (visual.GroupBy == PortfolioGroupBy.None)
        {
            return [GroupOf(null, null, rows)];
        }

        List<IGrouping<string?, PortfolioProjectRow>> groups = [.. rows.GroupBy(r => KeyOf(visual.GroupBy, r))];
        // A scale keeps its own order; anything else runs from the largest group. The projects with no value come last.
        IEnumerable<IGrouping<string?, PortfolioProjectRow>> ordered = visual.GroupBy is PortfolioGroupBy.Stage or PortfolioGroupBy.Status or PortfolioGroupBy.OnPlan
            ? groups.OrderBy(g => g.Key is null).ThenBy(g => ScaleOrder(visual.GroupBy, g.First(), stages))
            : groups.OrderBy(g => g.Key is null).ThenByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal);
        return [.. ordered.Select(g => GroupOf(g.Key, visual.GroupBy == PortfolioGroupBy.Stage && g.Key is { } code ? stages?.Find(code)?.Name : null, [.. g]))];
    }

    private static string? KeyOf(PortfolioGroupBy groupBy, PortfolioProjectRow row)
    {
        Domain.Project.Project p = row.Project;
        return groupBy switch
        {
            PortfolioGroupBy.Portfolio => p.StrategicPortfolioItemId?.ToString(),
            PortfolioGroupBy.Initiative => p.StrategicInitiativeItemId?.ToString(),
            PortfolioGroupBy.Sector => p.ClassificationItemId.ToString(),
            PortfolioGroupBy.Department => p.DepartmentId.ToString(),
            PortfolioGroupBy.Entity => p.ExternalEntityId?.ToString(),
            PortfolioGroupBy.Status => Code(p.LifecycleState),
            PortfolioGroupBy.Stage => p.CurrentStageCode,
            PortfolioGroupBy.OnPlan => row.Indicators.OnPlan is { } onPlan ? Code(onPlan) : null,
            PortfolioGroupBy.Region => p.RegionItemId?.ToString(),
            PortfolioGroupBy.City => p.CityItemId?.ToString(),
            PortfolioGroupBy.Priority => p.PriorityItemId?.ToString(),
            PortfolioGroupBy.None => null,
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, "Unknown grouping."),
        };
    }

    /// <summary>A group's place on its scale: the stage order, the lifecycle order, or on track before late before stalled.</summary>
    private static int ScaleOrder(PortfolioGroupBy groupBy, PortfolioProjectRow row, ProjectStageSet? stages) =>
        groupBy == PortfolioGroupBy.Stage ? stages?.Stages.Select(s => s.Code).ToList().IndexOf(row.Project.CurrentStageCode ?? string.Empty) ?? 0
        : groupBy == PortfolioGroupBy.Status ? (int)row.Project.LifecycleState
        : (int)(row.Indicators.OnPlan ?? 0);

    private static decimal? ValueOf(PortfolioMeasure measure, PortfolioBreakdownRow figures, IReadOnlyList<PortfolioProjectRow> members) => measure switch
    {
        PortfolioMeasure.Projects => figures.Projects,
        PortfolioMeasure.InProgress => members.Count(r => r.Project.LifecycleState == ProjectLifecycleState.Active),
        PortfolioMeasure.ValueSar => figures.ValueSar is { } sar ? decimal.Parse(sar, System.Globalization.CultureInfo.InvariantCulture) : null,
        PortfolioMeasure.AveragePlanned => figures.AveragePlannedPercent,
        PortfolioMeasure.AverageActual => figures.AverageActualPercent,
        PortfolioMeasure.GapPoints => figures.GapPoints,
        PortfolioMeasure.NeedingFollowUp => figures.NeedingFollowUp,
        PortfolioMeasure.OverdueActions => members.Count(r => r.Indicators.ActionDue == ActionDueStatus.Overdue),
        _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, "Unknown measure."),
    };

    private static PortfolioVisualInput InputOf(PortfolioVisual v) =>
        new(new BilingualLabel(v.TitleAr, v.TitleEn), v.VisualType, [.. v.Measures.Select(ParseMeasure)], v.GroupBy, v.LayoutColumn, v.LayoutRow, v.LayoutWidth, v.LayoutHeight);

    private static PortfolioVisualInput Card(string ar, string en, short column, short row, IReadOnlyList<PortfolioMeasure> measures) =>
        new(new BilingualLabel(ar, en), PortfolioVisualType.Card, measures, PortfolioGroupBy.None, column, row, 4, 2);

    /// <summary>An enum value as the API and the database write it: <c>AveragePlanned</c> as <c>AVERAGE_PLANNED</c>.</summary>
    private static string Code<T>(T value)
        where T : struct, Enum => System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    private static PortfolioMeasure ParseMeasure(string code) => Enum.Parse<PortfolioMeasure>(code.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true);
}
