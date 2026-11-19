using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.FollowUp;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Insights;

/// <summary>One value a visual draws: a field of the data catalogue (<see cref="PortfolioFieldDescriptor.Key"/>) and its calculation.</summary>
public sealed record PortfolioFieldValue(string Field, PortfolioFieldAggregation Aggregation);

/// <summary>
/// A visual's filter: a CODE field key or a dimension key (<c>PROJECT.&lt;GROUP_BY&gt;</c>) and the values that pass. A project passes when its value is
/// one of them; a value not revealed, and a dimension's null key, never passes.
/// </summary>
public sealed record PortfolioFieldFilter(string Field, IReadOnlyList<string> Values);

/// <summary>What a visual grouped by a date field groups by: the year (key <c>2026</c>) or the month (key <c>2026-03</c>).</summary>
public enum PortfolioDateUnit
{
    Year = 1,
    Month = 2,
}

/// <summary>A chart's category axis (the 19 Nov 2026 axes): shown or hidden, and a title in both languages or none.</summary>
public sealed record PortfolioAxis(bool Show, BilingualLabel? Title);

/// <summary>
/// A chart's value axis (the 19 Nov 2026 axes): shown or hidden, a title or none, a start and an end (null = automatic; the start below the end when both
/// are given) and whether gridlines are drawn.
/// </summary>
public sealed record PortfolioValueAxis(bool Show, BilingualLabel? Title, decimal? Min, decimal? Max, bool Gridlines);

/// <summary>How a bar, column or line chart draws its axes (the 19 Nov 2026 axes, dashboard-designer-axes.md); absent means the defaults: both axes shown, no titles, an automatic range and gridlines.</summary>
public sealed record PortfolioAxes(PortfolioAxis Category, PortfolioValueAxis Value)
{
    /// <summary>Both axes shown, no titles, an automatic range and gridlines.</summary>
    public static PortfolioAxes Default { get; } = new(new PortfolioAxis(Show: true, Title: null), new PortfolioValueAxis(Show: true, Title: null, null, null, Gridlines: true));
}

/// <summary>
/// A visual as the designer sends it: a title in both languages, a type, one to three measures, a grouping and a place on the grid; or, as a
/// <see cref="PortfolioVisualType.Section"/>, one of the dashboard's own sections and no measure. Since 18 Nov 2026 a visual may draw
/// <paramref name="Values"/> of the data catalogue instead of measures, be grouped by a CODE field (<paramref name="GroupField"/>) and narrowed by
/// <paramref name="Filters"/>; grouped by a date field, it names the <paramref name="GroupDateUnit"/> (the 18 Nov 2026 more fields). A bar, column or line
/// chart may set its <paramref name="Axes"/> (the 19 Nov 2026 axes). A <see cref="PortfolioSection.DashboardWidget"/> section names the governed widget it
/// draws by its <paramref name="WidgetCode"/> (the 19 Nov 2026 widgets); any other visual names none. A <see cref="PortfolioVisualType.Container"/> draws
/// nothing of its own and holds <paramref name="Children"/> (the 19 Nov 2026 containers), placed on its inner grid; any other visual holds none.
/// </summary>
public sealed record PortfolioVisualInput(
    BilingualLabel Title, PortfolioVisualType VisualType, IReadOnlyList<PortfolioMeasure> Measures, PortfolioGroupBy GroupBy, short LayoutColumn, short LayoutRow, short LayoutWidth,
    short LayoutHeight, PortfolioSection? Section = null, IReadOnlyList<PortfolioFieldValue>? Values = null, string? GroupField = null,
    IReadOnlyList<PortfolioFieldFilter>? Filters = null, PortfolioDateUnit? GroupDateUnit = null, PortfolioAxes? Axes = null, string? WidgetCode = null,
    IReadOnlyList<PortfolioVisualInput>? Children = null);

/// <summary>The fields the designer offers, in the register's order.</summary>
public sealed record PortfolioDesignFields(IReadOnlyList<PortfolioFieldDescriptor> Fields);

/// <summary>The whole design, in reading order.</summary>
public sealed record PortfolioDesignInput(IReadOnlyList<PortfolioVisualInput> Visuals)
{
    /// <summary>The visuals of a design, the containers' children counted.</summary>
    public const int MaxVisuals = 60;

    /// <summary>The children of one container.</summary>
    public const int MaxChildren = 20;
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
/// A section carries no groups of its own (an empty list): the dashboard draws it from the insights and the dashboard view, each under its own permission.
/// A visual of field values has a value per entry of <c>Values</c>, in order, and <c>OnPlan</c> null; it echoes its values, grouping field and filters, and
/// <c>ValueTypes</c> gives each value's field type, so a viewer without <c>DASHBOARD_DESIGN</c> (who cannot read the catalogue) formats it; null for fixed
/// measures and a section. A bar, column or line chart echoes its <c>Axes</c> as set (null for the defaults); a <c>DASHBOARD_WIDGET</c> section its
/// <c>WidgetCode</c>. A container carries no groups of its own (an empty list) and its <c>Children</c>, in order, each calculated as a top-level visual is;
/// <c>Children</c> is null on any other visual.
public sealed record PortfolioVisualResult(
    BilingualLabel Title, PortfolioVisualType VisualType, IReadOnlyList<PortfolioMeasure> Measures, PortfolioGroupBy GroupBy, short LayoutColumn, short LayoutRow, short LayoutWidth,
    short LayoutHeight, IReadOnlyList<PortfolioVisualGroup>? Groups, PortfolioSection? Section = null, IReadOnlyList<PortfolioFieldValue>? Values = null,
    string? GroupField = null, IReadOnlyList<PortfolioFieldFilter>? Filters = null, IReadOnlyList<PortfolioFieldType>? ValueTypes = null,
    PortfolioDateUnit? GroupDateUnit = null, PortfolioAxes? Axes = null, string? WidgetCode = null, IReadOnlyList<PortfolioVisualResult>? Children = null);

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

    /// <summary>The data catalogue's fields a visual may draw, group by or filter on.</summary>
    public PortfolioDesignFields GetFields();
}

/// <summary>The <c>project.portfolio_visual</c> table.</summary>
public interface IPortfolioVisualRepository
{
    public Task<IReadOnlyList<PortfolioVisual>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Deletes every visual and adds <paramref name="visuals"/>, in one save: a container before its children.</summary>
    public Task ReplaceAsync(IReadOnlyList<PortfolioVisual> visuals, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IPortfolioDesignService"/>
internal sealed partial class PortfolioDesignService(
    IPortfolioVisualRepository repository, PortfolioProjectRows projectRows, IAuthorizationEngine engine, OnPlanPolicy onPlanPolicy, IConfigurationResolver resolver,
    IPortfolioFieldReader fieldReader, TimeProvider timeProvider) : IPortfolioDesignService
{
    public const int MaxTitleLength = 100;
    private const int Columns = 12;
    private const int MaxHeight = 24;
    private const int MaxFilters = 5;
    private const int MaxFilterValues = 20;
    private const int MaxAxisTitleLength = 60;
    private const decimal AxisLimit = 1_000_000_000_000m;
    private const int MaxWidgetCodeLength = 50;
    private const string True = "TRUE";
    private const string False = "FALSE";
    private static readonly DateOnly UnixEpoch = new(1970, 1, 1);

    /// <summary>
    /// The dimension keys a filter takes: <c>PROJECT.&lt;GROUP_BY&gt;</c> for each project attribute a visual groups by (<c>PROJECT.STATUS</c>,
    /// <c>PROJECT.SECTOR</c>, …); a visual itself groups by one through <see cref="PortfolioVisualInput.GroupBy"/>.
    /// </summary>
    internal static IReadOnlyDictionary<string, PortfolioGroupBy> Dimensions { get; } = Enum.GetValues<PortfolioGroupBy>()
        .Where(g => g != PortfolioGroupBy.None).ToDictionary(g => $"PROJECT.{Code(g)}", StringComparer.Ordinal);

    /// <summary><c>project.portfolio_visual.data_fields</c> and <c>axes</c> as the API writes the same properties: camelCase, enumerations UPPER_SNAKE (R-19).</summary>
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
    };

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
        new(new BilingualLabel("المشاريع حسب الحالة", "Projects by status"), PortfolioVisualType.Bar, [PortfolioMeasure.Projects], PortfolioGroupBy.Status, 0, 9, 6, 5),
        new(new BilingualLabel("المشاريع حسب المرحلة", "Projects by stage"), PortfolioVisualType.Bar, [PortfolioMeasure.Projects], PortfolioGroupBy.Stage, 6, 9, 6, 5),
        new(new BilingualLabel("مؤشر الالتزام بالخطة", "On-plan indicator"), PortfolioVisualType.Donut, [PortfolioMeasure.Projects], PortfolioGroupBy.OnPlan, 0, 14, 6, 5),
        Section("نسبة إنجاز التحديثات حسب الجهة", "Update completion by entity", PortfolioSection.EntityCompletion, 6, 14, 6, 5),
        Section("حسب المحفظة", "By portfolio", PortfolioSection.PortfolioTable, 0, 19, 12, 6),
        Section("حسب المبادرة", "By initiative", PortfolioSection.InitiativeTable, 0, 25, 12, 6),
        Section("حسب القطاع", "By sector", PortfolioSection.SectorTable, 0, 31, 12, 8),
        Section("المشاريع حسب الحالة", "Projects by status", PortfolioSection.StatusTable, 0, 39, 6, 5),
        Section("المشاريع حسب المرحلة", "Projects by stage", PortfolioSection.CurrentStageTable, 6, 39, 6, 5),
        Section("مؤشر الالتزام بالخطة", "On-plan indicator", PortfolioSection.OnPlanCounts, 0, 44, 6, 4),
        Section("أسباب حاجة المشاريع إلى المتابعة", "Why projects need follow-up", PortfolioSection.FollowUpReasons, 6, 44, 6, 4),
        Section("أهم 10 مشاريع للمتابعة", "Top 10 to follow up", PortfolioSection.TopFollowUp, 0, 48, 12, 7),
        Section("المشاريع حسب الموقع", "Projects by location", PortfolioSection.ProjectLocations, 0, 55, 12, 7),
        Section("عناصر اللوحة", "Dashboard widgets", PortfolioSection.DashboardWidgets, 0, 62, 12, 17),
        Section("نسبة إنجاز التحديثات حسب الجهة (جدول)", "Update completion by entity (table)", PortfolioSection.EntityCompletionTable, 0, 79, 12, 8),
    ];

    public async Task<PortfolioDesignView> GetAsync(Guid callerId, PortfolioInsightFilters filters, CancellationToken cancellationToken)
    {
        IReadOnlyList<PortfolioVisual> saved = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return saved.Count == 0
            ? await ViewAsync(callerId, StandardDesign, filters, isStandard: true, updatedAt: null, cancellationToken).ConfigureAwait(false)
            : await ViewAsync(callerId, Nested(saved), filters, isStandard: false, saved.Max(v => v.UpdatedAt), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The saved rows as the design: the top-level visuals in reading order, each container holding its children in theirs.</summary>
    internal static IReadOnlyList<PortfolioVisualInput> Nested(IReadOnlyList<PortfolioVisual> saved)
    {
        ILookup<Guid?, PortfolioVisual> byParent = saved.OrderBy(v => v.SortOrder).ToLookup(v => v.ParentId);
        return [.. byParent[null].Select(v => v.VisualType == PortfolioVisualType.Container
            ? InputOf(v) with { Children = [.. byParent[v.Id].Select(InputOf)] }
            : InputOf(v))];
    }

    public async Task<AdministrationResult<PortfolioDesignView>> PreviewAsync(
        Guid callerId, PortfolioDesignInput design, PortfolioInsightFilters filters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(design);
        return Issue(design, fieldReader.Fields) is { } issue
            ? AdministrationError.Rule(ProjectErrorCodes.PortfolioDesignInvalid, issue)
            : await ViewAsync(callerId, design.Visuals, filters, isStandard: false, updatedAt: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<PortfolioDesignView>> SaveAsync(Guid callerId, PortfolioDesignInput design, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(design);
        if (Issue(design, fieldReader.Fields) is { } issue)
        {
            return AdministrationError.Rule(ProjectErrorCodes.PortfolioDesignInvalid, issue);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        List<PortfolioVisual> rows = [];
        foreach (PortfolioVisualInput visual in design.Visuals)
        {
            PortfolioVisual container = RowOf(visual, parentId: null);
            rows.Add(container);
            foreach (PortfolioVisualInput child in visual.Children ?? [])
            {
                rows.Add(RowOf(child, container.Id));
            }
        }

        await repository.ReplaceAsync(rows, cancellationToken).ConfigureAwait(false);
        return await ViewAsync(callerId, design.Visuals, new PortfolioInsightFilters(), isStandard: false, now, cancellationToken).ConfigureAwait(false);

        // The reading order runs over the whole design: a container, then its children, then the next visual.
        PortfolioVisual RowOf(PortfolioVisualInput v, Guid? parentId) =>
            new()
            {
                Id = Guid.CreateVersion7(now),
                SortOrder = (short)(rows.Count + 1),
                ParentId = parentId,
                TitleAr = v.Title.Ar.Trim(),
                TitleEn = v.Title.En.Trim(),
                VisualType = v.VisualType,
                Measures = [.. v.Measures.Select(Code)],
                Section = v.Section,
                GroupBy = v.GroupBy,
                LayoutColumn = v.LayoutColumn,
                LayoutRow = v.LayoutRow,
                LayoutWidth = v.LayoutWidth,
                LayoutHeight = v.LayoutHeight,
                DataFields = DataFieldsOf(v),
                Axes = AxesOf(v),
                WidgetCode = v.WidgetCode,
                CreatedAt = now,
                CreatedBy = callerId,
                UpdatedAt = now,
                UpdatedBy = callerId,
            };
    }

    public Task ResetAsync(CancellationToken cancellationToken) => repository.ReplaceAsync([], cancellationToken);

    public PortfolioDesignFields GetFields() => new(fieldReader.Fields);

    /// <summary>The permission that shows a grouping on the dashboard today: the headline and portfolios, the breakdowns, or stages and the on-plan indicator.</summary>
    internal static string PermissionOf(PortfolioGroupBy groupBy) => groupBy switch
    {
        PortfolioGroupBy.None or PortfolioGroupBy.Portfolio => PermissionCatalogue.PortfolioSummaryView,
        PortfolioGroupBy.Status or PortfolioGroupBy.Stage or PortfolioGroupBy.OnPlan => PermissionCatalogue.PortfolioStatusView,
        PortfolioGroupBy.Initiative or PortfolioGroupBy.Sector or PortfolioGroupBy.Department or PortfolioGroupBy.Entity or PortfolioGroupBy.Region
            or PortfolioGroupBy.City or PortfolioGroupBy.Priority => PermissionCatalogue.PortfolioBreakdownView,
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, "Unknown grouping."),
    };

    /// <summary>
    /// What is wrong with a design, or null. Visuals may not overlap; a card and a gauge show one figure, the others a figure per group. A field a visual names
    /// is one of <paramref name="fields"/>, the catalogue the designer offers (the 18 Nov 2026 data fields, rule 6). A container's children (the 19 Nov 2026
    /// containers) obey every rule a top-level visual obeys, sections and widget codes staying unique over the whole design, and fit, without overlapping,
    /// on the container's inner grid.
    /// </summary>
    internal static FieldIssue? Issue(PortfolioDesignInput design, IReadOnlyList<PortfolioFieldDescriptor> fields)
    {
        if (design.Visuals is not { Count: > 0 } visuals || visuals.Sum(v => 1 + (v.Children?.Count ?? 0)) > PortfolioDesignInput.MaxVisuals)
        {
            return new FieldIssue("visuals", design.Visuals is null || design.Visuals.Count == 0 ? FieldIssue.Required : FieldIssue.OutsideRange);
        }

        Dictionary<string, PortfolioFieldDescriptor> catalogue = fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        HashSet<(int Column, int Row)> taken = [];
        HashSet<PortfolioSection> sections = [];
        HashSet<string> widgetCodes = new(StringComparer.Ordinal);
        for (int index = 0; index < visuals.Count; index++)
        {
            PortfolioVisualInput v = visuals[index];
            string at = $"visuals[{index}]";
            FieldIssue? issue = (v.Children is not null && v.VisualType != PortfolioVisualType.Container ? new FieldIssue("children", FieldIssue.NotAllowed) : null)
                ?? VisualIssue(v, catalogue, sections, widgetCodes)
                ?? PlaceIssue(v, Columns, rows: null, taken);
            if (issue is not null)
            {
                return new FieldIssue($"{at}.{issue.Field}", issue.Code);
            }

            if (ChildrenIssue(v, catalogue, sections, widgetCodes) is { } childIssue)
            {
                return new FieldIssue($"{at}.{childIssue.Field}", childIssue.Code);
            }
        }

        return null;
    }

    /// <summary>
    /// What is wrong with a container's children, or null (their field relative to the container): at most twenty; none a container itself (one level
    /// only) or holding children; each a valid visual placed on the container's inner grid — its width's columns and its height less the title bar's row —
    /// without covering a sibling.
    /// </summary>
    private static FieldIssue? ChildrenIssue(
        PortfolioVisualInput container, Dictionary<string, PortfolioFieldDescriptor> catalogue, HashSet<PortfolioSection> sections, HashSet<string> widgetCodes)
    {
        if (container.Children is not { } children)
        {
            return null;
        }

        if (children.Count > PortfolioDesignInput.MaxChildren)
        {
            return new FieldIssue("children", FieldIssue.OutsideRange);
        }

        HashSet<(int Column, int Row)> taken = [];
        for (int index = 0; index < children.Count; index++)
        {
            PortfolioVisualInput child = children[index];
            FieldIssue? issue = child.VisualType == PortfolioVisualType.Container ? new FieldIssue("visualType", FieldIssue.NotAllowed)
                : child.Children is not null ? new FieldIssue("children", FieldIssue.NotAllowed)
                : VisualIssue(child, catalogue, sections, widgetCodes) ?? PlaceIssue(child, container.LayoutWidth, container.LayoutHeight - 1, taken);
            if (issue is not null)
            {
                return new FieldIssue($"children[{index}].{issue.Field}", issue.Code);
            }
        }

        return null;
    }

    /// <summary>What is wrong with one visual apart from its place, or null (its field relative to the visual): its title, its type and what it draws.</summary>
    private static FieldIssue? VisualIssue(
        PortfolioVisualInput v, Dictionary<string, PortfolioFieldDescriptor> catalogue, HashSet<PortfolioSection> sections, HashSet<string> widgetCodes) =>
        v.Title is null || string.IsNullOrWhiteSpace(v.Title.Ar) || string.IsNullOrWhiteSpace(v.Title.En) ? new FieldIssue("title", FieldIssue.Required)
        : v.Title.Ar.Trim().Length > MaxTitleLength || v.Title.En.Trim().Length > MaxTitleLength ? new FieldIssue("title", FieldIssue.OutsideRange)
        : !Enum.IsDefined(v.VisualType) || !Enum.IsDefined(v.GroupBy) ? new FieldIssue("visualType", FieldIssue.NotAllowed)
        : v.VisualType == PortfolioVisualType.Container ? ContainerIssue(v)
        : (v.VisualType == PortfolioVisualType.Section || v.Section is not null ? SectionIssue(v, sections) : DrawingIssue(v, catalogue))
            ?? AxesIssue(v)
            ?? WidgetCodeIssue(v, widgetCodes);

    /// <summary>
    /// What is wrong with a container itself, or null (the 19 Nov 2026 containers): it draws nothing — no measure, value, grouping, filter, axes, section
    /// or widget code — and is at least two rows tall, its title bar and a row for its children.
    /// </summary>
    private static FieldIssue? ContainerIssue(PortfolioVisualInput v) =>
        (v.Measures?.Count ?? 0) != 0 ? new("measures", FieldIssue.NotAllowed)
        : (v.Values?.Count ?? 0) != 0 ? new("values", FieldIssue.NotAllowed)
        : v.GroupBy != PortfolioGroupBy.None ? new("groupBy", FieldIssue.NotAllowed)
        : v.GroupField is not null ? new("groupField", FieldIssue.NotAllowed)
        : v.GroupDateUnit is not null ? new("groupDateUnit", FieldIssue.NotAllowed)
        : (v.Filters?.Count ?? 0) != 0 ? new("filters", FieldIssue.NotAllowed)
        : v.Axes is not null ? new("axes", FieldIssue.NotAllowed)
        : v.Section is not null ? new("section", FieldIssue.NotAllowed)
        : v.WidgetCode is not null ? new("widgetCode", FieldIssue.NotAllowed)
        : v.LayoutHeight < 2 ? new("layout", FieldIssue.OutsideRange)
        : null;

    /// <summary>
    /// What is wrong with a visual's place, or null: inside <paramref name="columns"/> columns and, on a container's inner grid, <paramref name="rows"/>
    /// rows (the dashboard itself has no last row); at most 24 rows tall; covering no cell of <paramref name="taken"/>, which it then takes.
    /// </summary>
    private static FieldIssue? PlaceIssue(PortfolioVisualInput v, int columns, int? rows, HashSet<(int Column, int Row)> taken)
    {
        if (v.LayoutColumn < 0 || v.LayoutWidth < 1 || v.LayoutColumn + v.LayoutWidth > columns || v.LayoutRow < 0 || v.LayoutHeight is < 1 or > MaxHeight
            || v.LayoutRow + v.LayoutHeight > rows)
        {
            return new FieldIssue("layout", FieldIssue.OutsideRange);
        }

        for (int column = v.LayoutColumn; column < v.LayoutColumn + v.LayoutWidth; column++)
        {
            for (int row = v.LayoutRow; row < v.LayoutRow + v.LayoutHeight; row++)
            {
                if (!taken.Add((column, row)))
                {
                    return new FieldIssue("layout", FieldIssue.InUse);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// What is wrong with a section, or null: it names a section, once in the design, as a <see cref="PortfolioVisualType.Section"/>, with no measure, field
    /// value, grouping or filter (it draws neither, rule 1). <see cref="PortfolioSection.DashboardWidget"/> may be placed more than once, one per widget
    /// (<see cref="WidgetCodeIssue"/>).
    /// </summary>
    private static FieldIssue? SectionIssue(PortfolioVisualInput v, HashSet<PortfolioSection> sections) =>
        v.VisualType != PortfolioVisualType.Section || v.Section is not { } section || !Enum.IsDefined(section) ? new("section", FieldIssue.NotAllowed)
        : (v.Measures?.Count ?? 0) != 0 || v.GroupBy != PortfolioGroupBy.None || (v.Values?.Count ?? 0) != 0 || v.GroupField is not null || (v.Filters?.Count ?? 0) != 0
            || v.GroupDateUnit is not null
            ? new("section", FieldIssue.NotAllowed)
        : section != PortfolioSection.DashboardWidget && !sections.Add(section) ? new("section", FieldIssue.Duplicate)
        : null;

    /// <summary>
    /// What is wrong with a visual's axes, or null (the 19 Nov 2026 axes): only a bar, column or line takes them; a title has both languages, each at most 60
    /// characters once trimmed; a start and an end lie within ±10¹², and the start is below the end when both are given.
    /// </summary>
    private static FieldIssue? AxesIssue(PortfolioVisualInput v) =>
        v.Axes is not { } axes ? null
        : v.VisualType is not (PortfolioVisualType.Bar or PortfolioVisualType.Column or PortfolioVisualType.Line) ? new("axes", FieldIssue.NotAllowed)
        : axes.Category is null ? new("axes.category", FieldIssue.Required)
        : axes.Value is null ? new FieldIssue("axes.value", FieldIssue.Required)
        : TitleIssue(axes.Category.Title, "axes.category.title") ?? TitleIssue(axes.Value.Title, "axes.value.title")
            ?? (axes.Value.Min is { } min && Math.Abs(min) > AxisLimit ? new FieldIssue("axes.value.min", FieldIssue.OutsideRange) : null)
            ?? (axes.Value.Max is { } max && Math.Abs(max) > AxisLimit ? new FieldIssue("axes.value.max", FieldIssue.OutsideRange) : null)
            ?? (axes.Value is { Min: { } start, Max: { } end } && start >= end ? new FieldIssue("axes.value.max", FieldIssue.OutsideRange) : null);

    /// <summary>
    /// What is wrong with a visual's widget code, or null (the 19 Nov 2026 widgets): a <see cref="PortfolioSection.DashboardWidget"/> section names one, 1–50
    /// characters of <c>[A-Za-z0-9_.-]</c>, no other visual of the design naming the same; any other visual names none. Only the shape is checked: the Project
    /// module does not read the Dashboards module's definitions, and a code the dashboard no longer has draws nothing.
    /// </summary>
    private static FieldIssue? WidgetCodeIssue(PortfolioVisualInput v, HashSet<string> widgetCodes) =>
        v.Section != PortfolioSection.DashboardWidget ? v.WidgetCode is null ? null : new("widgetCode", FieldIssue.NotAllowed)
        : string.IsNullOrEmpty(v.WidgetCode) ? new("widgetCode", FieldIssue.Required)
        : v.WidgetCode.Length > MaxWidgetCodeLength ? new("widgetCode", FieldIssue.OutsideRange)
        : !WidgetCodeShape().IsMatch(v.WidgetCode) ? new("widgetCode", FieldIssue.NotAllowed)
        : !widgetCodes.Add(v.WidgetCode) ? new("widgetCode", FieldIssue.Duplicate)
        : null;

    /// <summary>A title is a <see cref="BilingualLabel"/>, so both languages are there and not blank; each fits once trimmed.</summary>
    private static FieldIssue? TitleIssue(BilingualLabel? title, string field) =>
        title is not null && (title.Ar.Trim().Length > MaxAxisTitleLength || title.En.Trim().Length > MaxAxisTitleLength) ? new(field, FieldIssue.OutsideRange) : null;

    /// <summary>
    /// What is wrong with what a chart, card, line or table draws, or null (its field relative to the visual): the fixed measures or the field values, never both
    /// (rule 1), then its grouping (rule 4), its grain (rule 7) and its filters (rule 5). A line draws field values only.
    /// </summary>
    private static FieldIssue? DrawingIssue(PortfolioVisualInput v, Dictionary<string, PortfolioFieldDescriptor> catalogue)
    {
        FieldIssue? drawn = v.Values is { Count: > 0 } values
            ? (v.Measures?.Count ?? 0) != 0 ? new("values", FieldIssue.NotAllowed) : ValuesIssue(v.VisualType, values, catalogue)
            : v.VisualType == PortfolioVisualType.Line ? (v.Measures?.Count ?? 0) != 0 ? new("measures", FieldIssue.NotAllowed) : new("values", FieldIssue.Required)
            : MeasuresIssue(v);
        return drawn ?? GroupingIssue(v, catalogue) ?? GrainIssue(v, catalogue) ?? FiltersIssue(v, catalogue);
    }

    /// <summary>
    /// What is wrong with the measures, or null: one to three, each once; a bar, pie, donut and gauge draw one measure, a card two (the second under the
    /// first), columns and a table three; a gauge draws a percentage, a pie and a donut a total that adds up (a share of an average means nothing).
    /// </summary>
    private static FieldIssue? MeasuresIssue(PortfolioVisualInput v) =>
        v.Measures is not { Count: > 0 and <= 3 } measures || measures.Any(m => !Enum.IsDefined(m))
            ? new("measures", v.Measures is null || v.Measures.Count == 0 ? FieldIssue.Required : FieldIssue.OutsideRange)
        : measures.Distinct().Count() != measures.Count ? new("measures", FieldIssue.Duplicate)
        : measures.Count > MaxDrawn(v.VisualType, fieldValues: false)
            || (v.VisualType == PortfolioVisualType.Gauge && measures[0] is not (PortfolioMeasure.AveragePlanned or PortfolioMeasure.AverageActual))
            || (v.VisualType is PortfolioVisualType.Pie or PortfolioVisualType.Donut
                && measures[0] is PortfolioMeasure.AveragePlanned or PortfolioMeasure.AverageActual or PortfolioMeasure.GapPoints)
            ? new("measures", FieldIssue.NotAllowed)
        : null;

    /// <summary>
    /// What is wrong with the field values, or null (rules 2, 3 and 6): as many as the type draws (a table up to six, a line three), each a field the designer
    /// offers with an aggregation its type allows, no two alike; a gauge draws a percentage's average, least or greatest or a yes share, a pie and a donut a
    /// count, a sum or a count of yeses.
    /// </summary>
    private static FieldIssue? ValuesIssue(PortfolioVisualType type, IReadOnlyList<PortfolioFieldValue> values, Dictionary<string, PortfolioFieldDescriptor> catalogue)
    {
        if (values.Count > MaxDrawn(type, fieldValues: true))
        {
            return new("values", FieldIssue.OutsideRange);
        }

        if (values.Any(v => v?.Field is null || !catalogue.TryGetValue(v.Field, out PortfolioFieldDescriptor? field) || !field.Aggregations.Contains(v.Aggregation)))
        {
            return new("values", FieldIssue.NotAllowed);
        }

        if (values.Distinct().Count() != values.Count)
        {
            return new("values", FieldIssue.Duplicate);
        }

        PortfolioFieldValue first = values[0];
        PortfolioFieldType firstType = catalogue[first.Field].Type;
        bool fits = type switch
        {
            PortfolioVisualType.Gauge =>
                (firstType == PortfolioFieldType.Percent && first.Aggregation is PortfolioFieldAggregation.Average or PortfolioFieldAggregation.Min or PortfolioFieldAggregation.Max)
                || (firstType == PortfolioFieldType.Boolean && first.Aggregation == PortfolioFieldAggregation.ShareTrue),
            PortfolioVisualType.Pie or PortfolioVisualType.Donut =>
                first.Aggregation is PortfolioFieldAggregation.Count or PortfolioFieldAggregation.Sum or PortfolioFieldAggregation.CountTrue,
            PortfolioVisualType.Card or PortfolioVisualType.Bar or PortfolioVisualType.Column or PortfolioVisualType.Table or PortfolioVisualType.Line
                or PortfolioVisualType.Section or PortfolioVisualType.Container => true,
            _ => true,
        };
        return fits ? null : new("values", FieldIssue.NotAllowed);
    }

    /// <summary>
    /// How many measures or field values a type draws: a bar, pie, donut and gauge one, a card two, columns three, a table three measures or six values, a line
    /// three values and no measure.
    /// </summary>
    private static int MaxDrawn(PortfolioVisualType type, bool fieldValues) => type switch
    {
        PortfolioVisualType.Bar or PortfolioVisualType.Pie or PortfolioVisualType.Donut or PortfolioVisualType.Gauge => 1,
        PortfolioVisualType.Card => 2,
        PortfolioVisualType.Column => 3,
        PortfolioVisualType.Table => fieldValues ? 6 : 3,
        PortfolioVisualType.Line => fieldValues ? 3 : 0,
        PortfolioVisualType.Section or PortfolioVisualType.Container => 0,
        _ => 0,
    };

    /// <summary>
    /// What is wrong with the grouping, or null (rule 4): a card and a gauge are not grouped, a chart is — by a project attribute or by a CODE, BOOLEAN or date
    /// field the designer offers, never both — and a table either; a line only by a field. A date field names its <see cref="PortfolioVisualInput.GroupDateUnit"/>,
    /// any other grouping none.
    /// </summary>
    private static FieldIssue? GroupingIssue(PortfolioVisualInput v, Dictionary<string, PortfolioFieldDescriptor> catalogue)
    {
        bool single = v.VisualType is PortfolioVisualType.Card or PortfolioVisualType.Gauge;
        PortfolioFieldDescriptor? field = null;
        if (v.GroupField is { } key
            && (single || v.GroupBy != PortfolioGroupBy.None || !catalogue.TryGetValue(key, out field) || field.Type is not (PortfolioFieldType.Code or PortfolioFieldType.Boolean
                or PortfolioFieldType.Date or PortfolioFieldType.DateTime)))
        {
            return new("groupField", FieldIssue.NotAllowed);
        }

        bool byDate = field?.IsDate() == true;
        if (byDate && v.GroupDateUnit is null)
        {
            return new("groupDateUnit", FieldIssue.Required);
        }

        if (v.GroupDateUnit is { } unit && (!byDate || !Enum.IsDefined(unit)))
        {
            return new("groupDateUnit", FieldIssue.NotAllowed);
        }

        if (v.VisualType == PortfolioVisualType.Line)
        {
            return v.GroupBy != PortfolioGroupBy.None ? new("groupBy", FieldIssue.NotAllowed) : v.GroupField is null ? new("groupField", FieldIssue.Required) : null;
        }

        bool grouped = v.GroupBy != PortfolioGroupBy.None || v.GroupField is not null;
        return single == grouped && v.VisualType != PortfolioVisualType.Table ? new("groupBy", FieldIssue.NotAllowed) : null;
    }

    /// <summary>
    /// What is wrong with the grain, or null (rule 7): the values and the grouping field are all of one grain (fixed measures are of projects). A visual of
    /// snapshot values is a line, columns, bars or a table, grouped by a date or the CODE field of the values' own projection: figures over time.
    /// </summary>
    private static FieldIssue? GrainIssue(PortfolioVisualInput v, Dictionary<string, PortfolioFieldDescriptor> catalogue)
    {
        List<PortfolioFieldDescriptor> drawn = [.. (v.Values ?? []).Select(value => catalogue[value.Field])];
        PortfolioFieldDescriptor? group = v.GroupField is { } key ? catalogue[key] : null;
        string? projection = SnapshotProjectionOf(drawn);
        return projection is null ? group?.Grain == PortfolioFieldGrain.Snapshot ? new("groupField", FieldIssue.NotAllowed) : null
            : drawn.Any(f => f.ProjectionCode != projection) ? new("values", FieldIssue.NotAllowed)
            : v.VisualType is not (PortfolioVisualType.Line or PortfolioVisualType.Column or PortfolioVisualType.Bar or PortfolioVisualType.Table)
                ? new("visualType", FieldIssue.NotAllowed)
            : group is null ? new("groupField", FieldIssue.Required)
            : group.ProjectionCode != projection || group.Type is not (PortfolioFieldType.Code or PortfolioFieldType.Date or PortfolioFieldType.DateTime)
                ? new("groupField", FieldIssue.NotAllowed)
            : null;
    }

    /// <summary>The snapshot projection a visual's values are read from, or null for a visual of projects.</summary>
    private static string? SnapshotProjectionOf(IEnumerable<PortfolioFieldDescriptor> values) =>
        values.FirstOrDefault(f => f.Grain == PortfolioFieldGrain.Snapshot)?.ProjectionCode;

    /// <summary>
    /// What is wrong with the filters, or null (rule 5): at most five, each on a dimension (<see cref="Dimensions"/>) or a project-grain CODE, BOOLEAN or date
    /// field the designer offers — or, on a visual of snapshots, its own projection's CODE field — with one to twenty values, each of its kind's shape: a code, an
    /// id, <c>TRUE</c> or <c>FALSE</c>, or a year.
    /// </summary>
    private static FieldIssue? FiltersIssue(PortfolioVisualInput v, Dictionary<string, PortfolioFieldDescriptor> catalogue)
    {
        if (v.Filters is not { } filters)
        {
            return null;
        }

        if (filters.Count > MaxFilters)
        {
            return new("filters", FieldIssue.OutsideRange);
        }

        string? snapshots = SnapshotProjectionOf((v.Values ?? []).Select(value => catalogue[value.Field]));
        foreach (PortfolioFieldFilter filter in filters)
        {
            if (filter?.Field is null || filter.Values is not { Count: > 0 } values)
            {
                return new("filters", FieldIssue.Required);
            }

            FilterShape? shape = Dimensions.TryGetValue(filter.Field, out PortfolioGroupBy dimension)
                ? dimension is PortfolioGroupBy.Status or PortfolioGroupBy.Stage or PortfolioGroupBy.OnPlan ? FilterShape.Code : FilterShape.Id
                : catalogue.GetValueOrDefault(filter.Field) is { } field ? ShapeOf(field, snapshots) : null;
            if (shape is not { } kind)
            {
                return new("filters", FieldIssue.NotAllowed);
            }

            if (values.Count > MaxFilterValues)
            {
                return new("filters", FieldIssue.OutsideRange);
            }

            if (values.Any(value => value is null || !Fits(kind, value)))
            {
                return new("filters", FieldIssue.NotAllowed);
            }
        }

        return null;
    }

    /// <summary>
    /// The values a filter on <paramref name="field"/> takes, or null when it takes none: a CODE field's codes, a yes/no's <c>TRUE</c> or <c>FALSE</c>, a
    /// date's years; of a snapshot projection, only the CODE field of the visual's own (<paramref name="snapshots"/>).
    /// </summary>
    private static FilterShape? ShapeOf(PortfolioFieldDescriptor field, string? snapshots) =>
        field.Grain == PortfolioFieldGrain.Snapshot
            ? field.ProjectionCode == snapshots && field.Type == PortfolioFieldType.Code ? FilterShape.Code : null
            : field.Type switch
            {
                PortfolioFieldType.Code => FilterShape.Code,
                PortfolioFieldType.Boolean => FilterShape.Boolean,
                PortfolioFieldType.Date or PortfolioFieldType.DateTime => FilterShape.Year,
                PortfolioFieldType.Count or PortfolioFieldType.Percent or PortfolioFieldType.Days or PortfolioFieldType.Sar => null,
                _ => null,
            };

    private static bool Fits(FilterShape shape, string value) => shape switch
    {
        FilterShape.Code => CodeShape().IsMatch(value),
        FilterShape.Id => Guid.TryParseExact(value, "D", out _),
        FilterShape.Boolean => value is True or False,
        FilterShape.Year => YearShape().IsMatch(value),
        _ => false,
    };

    private async Task<PortfolioDesignView> ViewAsync(
        Guid callerId, IReadOnlyList<PortfolioVisualInput> visuals, PortfolioInsightFilters filters, bool isStandard, DateTimeOffset? updatedAt, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        // What is calculated: every visual drawing figures, a container's children as the top-level visuals; a section and a container draw none of their own.
        List<PortfolioVisualInput> drawn = [.. visuals.SelectMany(v => (IEnumerable<PortfolioVisualInput>)[v, .. v.Children ?? []])
            .Where(v => v.Section is null && v.VisualType != PortfolioVisualType.Container)];
        bool amounts = !(await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.FinancialViewSensitive, cancellationToken).ConfigureAwait(false)).IsEmpty;
        OnPlanThresholds thresholds = await onPlanPolicy.ThresholdsAsync(now, cancellationToken).ConfigureAwait(false);
        ProjectStageSet? stages = drawn.Any(v => v.GroupBy == PortfolioGroupBy.Stage)
            ? await resolver.ResolveStagesAsync(now, cancellationToken).ConfigureAwait(false)
            : null;

        // Each permission's projects are read once, however many visuals use it.
        Dictionary<string, IReadOnlyList<PortfolioProjectRow>?> rowsByPermission = new(StringComparer.Ordinal);
        foreach (string permission in drawn.Select(v => PermissionOf(v.GroupBy)).Distinct(StringComparer.Ordinal))
        {
            RecordScope scope = await engine.GetRecordScopeAsync(callerId, permission, cancellationToken).ConfigureAwait(false);
            rowsByPermission[permission] = scope.IsEmpty ? null : await projectRows.ListAsync(scope, filters, cancellationToken).ConfigureAwait(false);
        }

        // The data fields: one read for every field any visual names, over every project of the population, each cell authorised as a report cell.
        List<string> keys = [.. drawn.SelectMany(FieldKeysOf).Distinct(StringComparer.Ordinal)];
        List<Guid> projectIds = [.. rowsByPermission.Values.OfType<IReadOnlyList<PortfolioProjectRow>>().SelectMany(rows => rows).Select(r => r.Project.Id).Distinct()];
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells = keys.Count == 0 || projectIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>>()
            : await fieldReader.ReadAsync(callerId, projectIds, keys, now, cancellationToken).ConfigureAwait(false);

        Dictionary<string, PortfolioFieldDescriptor> catalogue = fieldReader.Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        Dictionary<string, PortfolioFieldType> types = catalogue.ToDictionary(f => f.Key, f => f.Value.Type, StringComparer.Ordinal);
        return new PortfolioDesignView(isStandard, updatedAt, [.. visuals.Select(v => ResultOf(v, GroupsOf, types))]);

        IReadOnlyList<PortfolioVisualGroup>? GroupsOf(PortfolioVisualInput v) =>
            rowsByPermission[PermissionOf(v.GroupBy)] is { } rows ? Groups(v, rows, stages, amounts, thresholds, cells, catalogue) : null;
    }

    /// <summary>
    /// A visual with its figures, <paramref name="groupsOf"/> calculating them (null without the grouping's permission): a section and a container carry none
    /// of their own; a container carries its children, each calculated as a top-level visual is.
    /// </summary>
    internal static PortfolioVisualResult ResultOf(
        PortfolioVisualInput v, Func<PortfolioVisualInput, IReadOnlyList<PortfolioVisualGroup>?> groupsOf, IReadOnlyDictionary<string, PortfolioFieldType> types) =>
        v.VisualType == PortfolioVisualType.Container
            ? new PortfolioVisualResult(
                v.Title, v.VisualType, [], PortfolioGroupBy.None, v.LayoutColumn, v.LayoutRow, v.LayoutWidth, v.LayoutHeight, [],
                Children: [.. (v.Children ?? []).Select(child => ResultOf(child, groupsOf, types))])
        : v.Section is { } section
            ? new PortfolioVisualResult(
                v.Title, v.VisualType, [], PortfolioGroupBy.None, v.LayoutColumn, v.LayoutRow, v.LayoutWidth, v.LayoutHeight, [], section, WidgetCode: v.WidgetCode)
        : new PortfolioVisualResult(
            v.Title, v.VisualType, v.Measures, v.GroupBy, v.LayoutColumn, v.LayoutRow, v.LayoutWidth, v.LayoutHeight, groupsOf(v), null, v.Values, v.GroupField,
            v.Filters, ValueTypesOf(v, types), v.GroupDateUnit, v.Axes);

    /// <summary>
    /// Each value's field type, in order; null for a visual of fixed measures. A field the register no longer holds (a design saved before it went) has only
    /// unrevealed cells, so its value is a count of 0 or not available, and it is typed <see cref="PortfolioFieldType.Count"/>.
    /// </summary>
    internal static IReadOnlyList<PortfolioFieldType>? ValueTypesOf(PortfolioVisualInput v, IReadOnlyDictionary<string, PortfolioFieldType> types) =>
        v.Values is { Count: > 0 } values ? [.. values.Select(value => types.GetValueOrDefault(value.Field, PortfolioFieldType.Count))] : null;

    /// <summary>The field keys a visual reads: its values' fields, its grouping field and its filters' fields that are not dimensions.</summary>
    private static IEnumerable<string> FieldKeysOf(PortfolioVisualInput v) =>
        (v.Values ?? []).Select(value => value.Field)
            .Concat(v.GroupField is { } key ? [key] : [])
            .Concat((v.Filters ?? []).Select(f => f.Field).Where(field => !Dimensions.ContainsKey(field)));

    /// <summary>
    /// A visual's groups: its projects narrowed by its filters, grouped by a project attribute or a field's value. A visual of snapshot values (rule 7) is
    /// calculated over its projects' published snapshots instead, narrowed by its snapshot filters. Field values are calculated over the revealed cells only
    /// (<see cref="Aggregate"/>); <see cref="PortfolioVisualGroup.Measured"/> and <see cref="PortfolioVisualGroup.ValueEntered"/> are then the rows (projects
    /// or snapshots) whose first value is revealed, and <see cref="PortfolioVisualGroup.Projects"/> the distinct projects.
    /// </summary>
    internal static IReadOnlyList<PortfolioVisualGroup> Groups(
        PortfolioVisualInput visual, IReadOnlyList<PortfolioProjectRow> rows, ProjectStageSet? stages, bool amounts, OnPlanThresholds thresholds,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells, IReadOnlyDictionary<string, PortfolioFieldDescriptor> catalogue)
    {
        PortfolioFieldDescriptor? FieldOf(string key) => catalogue.GetValueOrDefault(key);
        bool IsSnapshotField(string key) => FieldOf(key)?.Grain == PortfolioFieldGrain.Snapshot;
        IReadOnlyList<PortfolioFieldCell> CellsOf(PortfolioProjectRow row, string key) => cells.GetValueOrDefault(row.Project.Id)?.GetValueOrDefault(key) ?? [];

        bool snapshotVisual = (visual.Values ?? []).Any(v => IsSnapshotField(v.Field));
        List<PortfolioFieldFilter> projectFilters = [.. (visual.Filters ?? []).Where(f => !IsSnapshotField(f.Field))];
        List<PortfolioFieldFilter> snapshotFilters = [.. (visual.Filters ?? []).Where(f => IsSnapshotField(f.Field))];
        List<string> snapshotKeys = [.. FieldKeysOf(visual).Where(IsSnapshotField).Distinct(StringComparer.Ordinal)];

        bool Passes(PortfolioFieldFilter filter, FieldRow row) =>
            filter.Values.Contains(
                Dimensions.TryGetValue(filter.Field, out PortfolioGroupBy dimension) ? KeyOf(dimension, row.Project) : FilterKeyOf(FieldOf(filter.Field), row.Cell(filter.Field)),
                StringComparer.OrdinalIgnoreCase);

        List<FieldRow> projects = [.. rows.Select(r => new FieldRow(r, key => CellsOf(r, key) is [var first, ..] ? first.Value : null))
            .Where(r => projectFilters.All(f => Passes(f, r)))];
        List<FieldRow> members = snapshotVisual
            ? [.. projects.SelectMany(p => SnapshotsOf(p.Project, key => CellsOf(p.Project, key), snapshotKeys)).Where(r => snapshotFilters.All(f => Passes(f, r)))]
            : projects;

        PortfolioVisualGroup GroupOf(string? key, BilingualLabel? label, IReadOnlyList<FieldRow> group)
        {
            int distinct = group.Select(m => m.Project.Project.Id).Distinct().Count();
            if (visual.Values is { Count: > 0 } values)
            {
                int revealed = group.Count(m => m.Cell(values[0].Field) is not null);
                return new PortfolioVisualGroup(
                    key, label, distinct, revealed, revealed,
                    [.. values.Select(v => Aggregate(v.Aggregation, FieldOf(v.Field)?.Type ?? PortfolioFieldType.Count, group.Select(m => m.Cell(v.Field))))], null);
            }

            List<PortfolioProjectRow> projectRows = [.. group.Select(m => m.Project)];
            PortfolioBreakdownRow figures = PortfolioInsightsService.RowOf(null, null, projectRows, amounts, thresholds, sector: false);
            return new PortfolioVisualGroup(
                key, label, figures.Projects, figures.Measured, figures.ValueEntered, [.. visual.Measures.Select(m => ValueOf(m, figures, projectRows))], figures.OnPlan);
        }

        if (visual.GroupField is { } field)
        {
            // A date's groups run in time order; any other field's from the largest group. The rows whose value is not revealed come last.
            PortfolioFieldDescriptor? descriptor = FieldOf(field);
            List<IGrouping<string?, FieldRow>> byValue = [.. members.GroupBy(m => GroupKeyOf(descriptor, visual.GroupDateUnit, m.Cell(field)))];
            IEnumerable<IGrouping<string?, FieldRow>> ordered = descriptor?.IsDate() == true
                ? byValue.OrderBy(g => g.Key is null).ThenBy(g => g.Key, StringComparer.Ordinal)
                : byValue.OrderBy(g => g.Key is null).ThenByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal);
            return [.. ordered.Select(g => GroupOf(g.Key, null, [.. g]))];
        }

        if (visual.GroupBy == PortfolioGroupBy.None)
        {
            return [GroupOf(null, null, members)];
        }

        List<IGrouping<string?, FieldRow>> groups = [.. members.GroupBy(m => KeyOf(visual.GroupBy, m.Project))];
        // A scale keeps its own order; anything else runs from the largest group. The projects with no value come last.
        IEnumerable<IGrouping<string?, FieldRow>> scaled = visual.GroupBy is PortfolioGroupBy.Stage or PortfolioGroupBy.Status or PortfolioGroupBy.OnPlan
            ? groups.OrderBy(g => g.Key is null).ThenBy(g => ScaleOrder(visual.GroupBy, g.First().Project, stages))
            : groups.OrderBy(g => g.Key is null).ThenByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal);
        return [.. scaled.Select(g => GroupOf(g.Key, visual.GroupBy == PortfolioGroupBy.Stage && g.Key is { } code ? stages?.Find(code)?.Name : null, [.. g]))];
    }

    /// <summary>A project's published snapshots as rows, oldest first: each of <paramref name="keys"/> (snapshot-grain) read from that snapshot's cell.</summary>
    private static IEnumerable<FieldRow> SnapshotsOf(PortfolioProjectRow project, Func<string, IReadOnlyList<PortfolioFieldCell>> cellsOf, IReadOnlyCollection<string> keys)
    {
        Dictionary<(string Key, int Index), string?> byIndex = [];
        foreach (string key in keys)
        {
            foreach (PortfolioFieldCell cell in cellsOf(key))
            {
                if (cell.SnapshotIndex is { } index)
                {
                    byIndex[(key, index)] = cell.Value;
                }
            }
        }

        return byIndex.Keys.Select(k => k.Index).Distinct().Order().Select(index => new FieldRow(project, key => byIndex.GetValueOrDefault((key, index))));
    }

    /// <summary>A filter's comparable value of a field's cell: a yes/no as <c>TRUE</c> or <c>FALSE</c>, a date as its year, anything else as the source wrote it.</summary>
    internal static string? FilterKeyOf(PortfolioFieldDescriptor? field, string? value) =>
        value is null || field is null ? value
        : field.Type == PortfolioFieldType.Boolean ? value.ToUpperInvariant()
        : field.IsDate() ? (value.Length >= 4 ? value[..4] : null)
        : value;

    /// <summary>A group's key of a field's cell: a yes/no as <c>TRUE</c> or <c>FALSE</c>, a date as its year (<c>2026</c>) or month (<c>2026-03</c>), a code as is.</summary>
    internal static string? GroupKeyOf(PortfolioFieldDescriptor? field, PortfolioDateUnit? unit, string? value) =>
        value is null || field is null ? value
        : field.Type == PortfolioFieldType.Boolean ? value.ToUpperInvariant()
        : field.IsDate() ? unit == PortfolioDateUnit.Month ? (value.Length >= 7 ? value[..7] : null) : (value.Length >= 4 ? value[..4] : null)
        : value;

    /// <summary>
    /// A field value over a group's cells, the revealed ones only (null for a cell not revealed): <c>COUNT</c> counts them and <c>COUNT_TRUE</c> their yeses, 0
    /// when there is none; <c>SHARE_TRUE</c> is the yeses' share in percent; the others calculate over their numbers — a date's being its day number since
    /// 1970-01-01 — and are null, "not available" and never 0, when there is none. An average and a share keep two places, as the insights' averages do.
    /// </summary>
    internal static decimal? Aggregate(PortfolioFieldAggregation aggregation, PortfolioFieldType type, IEnumerable<string?> cells)
    {
        List<string> revealed = [.. cells.OfType<string>()];
        int yes = revealed.Count(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
        List<decimal> numbers = [.. revealed.Select(v => type is PortfolioFieldType.Date or PortfolioFieldType.DateTime ? DayNumber(v) : Number(v)).OfType<decimal>()];
        return aggregation switch
        {
            PortfolioFieldAggregation.Count => revealed.Count,
            PortfolioFieldAggregation.CountTrue => yes,
            PortfolioFieldAggregation.ShareTrue => revealed.Count == 0 ? null : decimal.Round(yes * 100m / revealed.Count, 2, MidpointRounding.AwayFromZero),
            PortfolioFieldAggregation.Sum => numbers.Count == 0 ? null : numbers.Sum(),
            PortfolioFieldAggregation.Average => numbers.Count == 0 ? null : decimal.Round(numbers.Average(), 2, MidpointRounding.AwayFromZero),
            PortfolioFieldAggregation.Min => numbers.Count == 0 ? null : numbers.Min(),
            PortfolioFieldAggregation.Max => numbers.Count == 0 ? null : numbers.Max(),
            _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown aggregation."),
        };
    }

    /// <summary>A source's date (<c>yyyy-MM-dd</c>, or a UTC moment starting so) as its day number since 1970-01-01; null for one that is not.</summary>
    private static decimal? DayNumber(string value) =>
        value.Length >= 10 && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date.DayNumber - UnixEpoch.DayNumber
            : null;

    /// <summary>A source's exact decimal string as a number; null for one that is not (a code).</summary>
    private static decimal? Number(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number) ? number : null;

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
        PortfolioMeasure.ValueSar => figures.ValueSar is { } sar ? decimal.Parse(sar, CultureInfo.InvariantCulture) : null,
        PortfolioMeasure.AveragePlanned => figures.AveragePlannedPercent,
        PortfolioMeasure.AverageActual => figures.AverageActualPercent,
        PortfolioMeasure.GapPoints => figures.GapPoints,
        PortfolioMeasure.NeedingFollowUp => figures.NeedingFollowUp,
        PortfolioMeasure.OverdueActions => members.Count(r => r.Indicators.ActionDue == ActionDueStatus.Overdue),
        _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, "Unknown measure."),
    };

    private static PortfolioVisualInput InputOf(PortfolioVisual v)
    {
        PortfolioVisualDataFields? data = v.DataFields is { } json ? JsonSerializer.Deserialize<PortfolioVisualDataFields>(json, StoredJson) : null;
        return new(
            new BilingualLabel(v.TitleAr, v.TitleEn), v.VisualType, [.. v.Measures.Select(ParseMeasure)], v.GroupBy, v.LayoutColumn, v.LayoutRow, v.LayoutWidth, v.LayoutHeight,
            v.Section, data?.Values, data?.GroupField, data?.Filters, data?.GroupDateUnit,
            v.Axes is { } axes ? JsonSerializer.Deserialize<PortfolioAxes>(axes, StoredJson) : null, v.WidgetCode);
    }

    /// <summary>What <c>data_fields</c> holds for a visual that names any data field; null for one that names none (fixed measures, a section, a container).</summary>
    private static string? DataFieldsOf(PortfolioVisualInput v) =>
        v.Values is { Count: > 0 } || v.GroupField is not null || v.Filters is { Count: > 0 } || v.GroupDateUnit is not null
            ? JsonSerializer.Serialize(new PortfolioVisualDataFields(v.Values, v.GroupField, v.Filters, v.GroupDateUnit), StoredJson)
            : null;

    /// <summary>
    /// What <c>axes</c> holds: the axes with their titles trimmed, or null when they are the defaults (a bar, column or line drawn as standard) or the visual
    /// takes none.
    /// </summary>
    internal static string? AxesOf(PortfolioVisualInput v)
    {
        if (v.Axes is not { } axes || v.VisualType is not (PortfolioVisualType.Bar or PortfolioVisualType.Column or PortfolioVisualType.Line))
        {
            return null;
        }

        PortfolioAxes trimmed = new(
            axes.Category with { Title = Trimmed(axes.Category.Title) },
            axes.Value with { Title = Trimmed(axes.Value.Title) });
        return trimmed == PortfolioAxes.Default ? null : JsonSerializer.Serialize(trimmed, StoredJson);

        static BilingualLabel? Trimmed(BilingualLabel? title) => title is null ? null : new BilingualLabel(title.Ar.Trim(), title.En.Trim());
    }

    private static PortfolioVisualInput Section(string ar, string en, PortfolioSection section, short column, short row, short width, short height) =>
        new(new BilingualLabel(ar, en), PortfolioVisualType.Section, [], PortfolioGroupBy.None, column, row, width, height, section);

    private static PortfolioVisualInput Card(string ar, string en, short column, short row, IReadOnlyList<PortfolioMeasure> measures) =>
        new(new BilingualLabel(ar, en), PortfolioVisualType.Card, measures, PortfolioGroupBy.None, column, row, 4, 2);

    /// <summary>An enum value as the API and the database write it: <c>AveragePlanned</c> as <c>AVERAGE_PLANNED</c>.</summary>
    private static string Code<T>(T value)
        where T : struct, Enum => JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    private static PortfolioMeasure ParseMeasure(string code) => Enum.Parse<PortfolioMeasure>(code.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true);

    /// <summary>A filter value of a CODE field, a status, a stage or an on-plan label.</summary>
    [GeneratedRegex("^[A-Z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();

    /// <summary>A governed widget's code, as the dashboard view carries it; its length is checked apart.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9_.-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex WidgetCodeShape();

    /// <summary>A filter value of a date field: a year.</summary>
    [GeneratedRegex("^[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex YearShape();

    /// <summary>The JSON of <c>data_fields</c>: the visual's values, grouping field and its date unit, and filters, as the API carries them.</summary>
    private sealed record PortfolioVisualDataFields(
        IReadOnlyList<PortfolioFieldValue>? Values, string? GroupField, IReadOnlyList<PortfolioFieldFilter>? Filters, PortfolioDateUnit? GroupDateUnit = null);

    /// <summary>A row a visual of fields calculates over: a project, or one published snapshot of it; <see cref="Cell"/> reads a field key's revealed value.</summary>
    private sealed record FieldRow(PortfolioProjectRow Project, Func<string, string?> Cell);

    /// <summary>What the values of a filter are: codes, ids (items, departments, entities), yes/no, or years.</summary>
    private enum FilterShape
    {
        Code = 1,
        Id = 2,
        Boolean = 3,
        Year = 4,
    }
}
