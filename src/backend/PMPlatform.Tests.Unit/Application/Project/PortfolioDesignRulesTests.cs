using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Dashboards;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Insights;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Tests.Unit.Application.Project;

/// <summary>
/// The 17 Nov 2026 dashboard designer: what a saved design may hold, and the permission each grouping is read under; and its 18 Nov 2026 data fields: the
/// catalogue it offers, rules 1 to 6 and the aggregations' arithmetic; and its 18 Nov 2026 more fields: PROJECT.PROJECT_PROFILE, yes/no and date fields, the
/// LINE visual, rule 7's grain and the period charts over the published history; and its 19 Nov 2026 axes: who takes them, their titles and range, and what is
/// stored; and its 19 Nov 2026 widgets: one DASHBOARD_WIDGET section per governed widget, named by its code; and its 19 Nov 2026 containers: rules 1 to 6
/// (dashboard-designer-containers.md), the saved rows nested back and the children calculated as top-level visuals; and its 20 Nov 2026 decimal layout: places
/// and sizes in steps of 0.1, overlaps by area (dashboard-designer-decimal-layout.md); and its 20 Nov 2026 table columns: which visuals hide columns and the
/// codes' shape (dashboard-designer-table-columns.md); and its 20 Nov 2026 Power BI-style table: the columns' rules and the table's calculation
/// (dashboard-designer-power-table.md); and its 20 Nov 2026 box sections: the on-plan indicator's and the follow-up reasons' boxes as figures
/// (dashboard-designer-box-sections.md).
/// </summary>
public sealed class PortfolioDesignRulesTests
{
    private static PortfolioVisualInput Visual(
        PortfolioVisualType type = PortfolioVisualType.Bar, PortfolioGroupBy groupBy = PortfolioGroupBy.Sector, decimal column = 0, decimal row = 0, decimal width = 6,
        decimal height = 4, params PortfolioMeasure[] measures) =>
        new(new BilingualLabel("المشاريع حسب القطاع", "Projects by sector"), type, measures.Length == 0 ? [PortfolioMeasure.Projects] : measures, groupBy, column, row, width, height);

    private const string Spend = "FINANCIAL_KPI.FINANCIAL_POSITION.ACTUAL_EXPENDITURE_TO_DATE";
    private const string Health = "PROGRESS.PROJECT_HEALTH_STATUS.OVERALL_HEALTH";
    private const string ActualPercent = "PROGRESS.PROJECT_HEALTH_STATUS.ACTUAL_PERCENT";
    private const string OpenRisks = "RISK.RISK_EXPOSURE.OPEN_RISKS";

    /// <summary>The catalogue the designer offers: the register's own, as the field reader builds it.</summary>
    private static readonly IReadOnlyList<PortfolioFieldDescriptor> Catalogue = [.. PortfolioFieldReader.Catalogue(DashboardProjections.Reportable.Select(p => p.ToDescriptor()))];

    private static FieldIssue? Issue(params PortfolioVisualInput[] visuals) => PortfolioDesignService.Issue(new PortfolioDesignInput(visuals), Catalogue);

    /// <summary>A visual drawing field values instead of measures.</summary>
    private static PortfolioVisualInput Drawing(
        PortfolioVisualType type = PortfolioVisualType.Bar, PortfolioGroupBy groupBy = PortfolioGroupBy.None, string? groupField = Health,
        IReadOnlyList<PortfolioFieldFilter>? filters = null, params PortfolioFieldValue[] values) =>
        new(new BilingualLabel("المصروف حسب الصحة", "Spend by health"), type, [], groupBy, 0, 0, 6, 5, null,
            values.Length == 0 ? [new PortfolioFieldValue(Spend, PortfolioFieldAggregation.Sum)] : values, groupField, filters);

    private static PortfolioFieldValue Value(string field, PortfolioFieldAggregation aggregation) => new(field, aggregation);

    [Fact]
    public void TheStandardDesignIsValid() => Assert.Null(Issue([.. PortfolioDesignService.StandardDesign]));

    [Fact]
    public void ADesignNeedsAVisual() => Assert.Equal(FieldIssue.Required, Issue()?.Code);

    [Fact]
    public void VisualsSideBySideFit() => Assert.Null(Issue(Visual(column: 0), Visual(column: 6)));

    [Fact]
    public void AVisualMayNotCoverAnother()
    {
        FieldIssue? issue = Issue(Visual(column: 0), Visual(column: 5, row: 3));

        Assert.Equal("visuals[1].layout", issue?.Field);
        Assert.Equal(FieldIssue.InUse, issue?.Code);
    }

    [Theory]
    [InlineData(7, 6)]
    [InlineData(-1, 4)]
    [InlineData(0, 13)]
    public void AVisualStaysInsideTheTwelveColumns(short column, short width) =>
        Assert.Equal(FieldIssue.OutsideRange, Issue(Visual(column: column, width: width))?.Code);

    /// <summary>The 20 Nov 2026 decimal layout: places and sizes in steps of 0.1.</summary>
    [Fact]
    public void PlacesAndSizesTakeOneDecimal()
    {
        Assert.Null(Issue(Visual(column: 0, width: 4.5m), Visual(column: 4.5m, width: 4.7m, height: 3.3m)));
        Assert.Null(Issue(Visual(column: 0.1m, row: 0.5m, width: 11.9m, height: 23.5m)));
        Assert.Null(Issue(Visual(column: 4.50m, width: 4.70m)));
    }

    [Theory]
    [InlineData("4.55", "0", "4", "4")]
    [InlineData("0", "4.55", "4", "4")]
    [InlineData("0", "0", "4.55", "4")]
    [InlineData("0", "0", "4", "4.01")]
    public void APlaceWithTwoDecimalsIsRefused(string column, string row, string width, string height) =>
        Assert.Equal(
            new FieldIssue("visuals[0].layout", FieldIssue.OutsideRange),
            Issue(Visual(column: Number(column), row: Number(row), width: Number(width), height: Number(height))));

    [Fact]
    public void TouchingEdgesDoNotOverlapButATenthDoes()
    {
        Assert.Null(Issue(Visual(column: 0, width: 4.5m), Visual(column: 4.5m, width: 4.5m)));
        Assert.Null(Issue(Visual(row: 0, height: 2.5m), Visual(row: 2.5m, height: 2)));
        Assert.Equal(new FieldIssue("visuals[1].layout", FieldIssue.InUse), Issue(Visual(column: 0, width: 4.5m), Visual(column: 4.4m, width: 4.5m)));
        Assert.Equal(new FieldIssue("visuals[1].layout", FieldIssue.InUse), Issue(Visual(row: 0, height: 2.5m), Visual(row: 2.4m, height: 2)));
    }

    [Fact]
    public void AChildAsWideAsItsDecimalContainerFits()
    {
        Assert.Null(Issue(Container(width: 4.5m, height: 3.5m, children: [SmallCard(0, 0, width: 4.5m, height: 2.5m)])));
        Assert.Equal(
            new FieldIssue("visuals[0].children[0].layout", FieldIssue.OutsideRange),
            Issue(Container(width: 4.5m, height: 3.5m, children: [SmallCard(0, 0, width: 4.6m, height: 2.5m)])));
        Assert.Equal(new FieldIssue("visuals[0].layout", FieldIssue.OutsideRange), Issue(Container(height: 1.9m)));
        Assert.Null(Issue(Container(height: 2.0m)));
    }

    [Theory]
    [InlineData("0", "1.0", "0", "1.0", true)]
    [InlineData("0", "12.0", "0", "24.0", true)]
    [InlineData("11.0", "1.0", "0", "1.0", true)]
    [InlineData("0", "0.9", "0", "4", false)]
    [InlineData("0", "12.1", "0", "4", false)]
    [InlineData("0", "4", "0", "0.9", false)]
    [InlineData("0", "4", "0", "24.1", false)]
    [InlineData("11.1", "1.0", "0", "1.0", false)]
    [InlineData("-0.1", "4", "0", "4", false)]
    [InlineData("0", "4", "-0.1", "4", false)]
    public void DecimalRangesKeepTheirWholeBounds(string column, string width, string row, string height, bool valid) =>
        Assert.Equal(
            valid ? null : new FieldIssue("visuals[0].layout", FieldIssue.OutsideRange),
            Issue(Visual(column: Number(column), row: Number(row), width: Number(width), height: Number(height))));

    private static decimal Number(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void ACardIsNotGrouped() =>
        Assert.Equal("visuals[0].groupBy", Issue(Visual(PortfolioVisualType.Card, PortfolioGroupBy.Sector, width: 4, height: 2))?.Field);

    [Fact]
    public void AChartIsGrouped() => Assert.Equal("visuals[0].groupBy", Issue(Visual(PortfolioVisualType.Pie, PortfolioGroupBy.None))?.Field);

    [Fact]
    public void ATableIsGroupedOrNot()
    {
        Assert.Null(Issue(Visual(PortfolioVisualType.Table, PortfolioGroupBy.None)));
        Assert.Null(Issue(Visual(PortfolioVisualType.Table, PortfolioGroupBy.Entity)));
    }

    [Fact]
    public void ABarDrawsOneMeasure() =>
        Assert.Equal("visuals[0].measures", Issue(Visual(measures: [PortfolioMeasure.Projects, PortfolioMeasure.ValueSar]))?.Field);

    [Fact]
    public void ColumnsDrawUpToThreeMeasures() =>
        Assert.Null(Issue(Visual(PortfolioVisualType.Column, measures: [PortfolioMeasure.AveragePlanned, PortfolioMeasure.AverageActual, PortfolioMeasure.GapPoints])));

    [Fact]
    public void AMeasureIsNamedOnce() =>
        Assert.Equal(FieldIssue.Duplicate, Issue(Visual(PortfolioVisualType.Column, measures: [PortfolioMeasure.Projects, PortfolioMeasure.Projects]))?.Code);

    [Fact]
    public void AGaugeDrawsAPercentage()
    {
        Assert.Null(Issue(Visual(PortfolioVisualType.Gauge, PortfolioGroupBy.None, measures: [PortfolioMeasure.AverageActual])));
        Assert.NotNull(Issue(Visual(PortfolioVisualType.Gauge, PortfolioGroupBy.None, measures: [PortfolioMeasure.Projects])));
    }

    [Fact]
    public void APieDrawsATotalThatAddsUp()
    {
        Assert.Null(Issue(Visual(PortfolioVisualType.Pie, measures: [PortfolioMeasure.ValueSar])));
        Assert.Equal("visuals[0].measures", Issue(Visual(PortfolioVisualType.Pie, measures: [PortfolioMeasure.AveragePlanned]))?.Field);
    }

    /// <summary>A blank title never reaches the rule (a BilingualLabel refuses one); a long one does.</summary>
    [Fact]
    public void ATitleIsAtMostAHundredCharacters()
    {
        PortfolioVisualInput titled = Visual() with { Title = new BilingualLabel("المشاريع", new string('x', 101)) };

        Assert.Equal(new FieldIssue("visuals[0].title", FieldIssue.OutsideRange), Issue(titled));
    }

    private static PortfolioVisualInput Placed(PortfolioSection section, short row, params PortfolioMeasure[] measures) =>
        new(new BilingualLabel("حسب المحفظة", "By portfolio"), PortfolioVisualType.Section, measures, PortfolioGroupBy.None, 0, row, 12, 4, section);

    /// <summary>18 Nov 2026: the whole dashboard is designed; each of its own sections is placed at most once and carries no measure.</summary>
    [Fact]
    public void ASectionIsPlacedOnceWithNoMeasure()
    {
        Assert.Null(Issue(Placed(PortfolioSection.PortfolioTable, 0), Placed(PortfolioSection.TopFollowUp, 4)));
        Assert.Equal(new FieldIssue("visuals[1].section", FieldIssue.Duplicate), Issue(Placed(PortfolioSection.PortfolioTable, 0), Placed(PortfolioSection.PortfolioTable, 4)));
        Assert.Equal("visuals[0].section", Issue(Placed(PortfolioSection.PortfolioTable, 0, PortfolioMeasure.Projects))?.Field);
        Assert.Equal("visuals[0].section", Issue(Visual() with { Section = PortfolioSection.PortfolioTable })?.Field);
    }

    /// <summary>Every section but DASHBOARD_WIDGET: the backend does not know the widget codes, so the standard design keeps DASHBOARD_WIDGETS for the designer to split.</summary>
    [Fact]
    public void TheStandardDesignPlacesEverySection() =>
        Assert.Equal(
            Enum.GetValues<PortfolioSection>().Where(s => s != PortfolioSection.DashboardWidget),
            PortfolioDesignService.StandardDesign.Select(v => v.Section).OfType<PortfolioSection>().Order());

    private static PortfolioVisualInput Widget(string? widgetCode, short row = 0) => Placed(PortfolioSection.DashboardWidget, row) with { WidgetCode = widgetCode };

    /// <summary>19 Nov 2026: a DASHBOARD_WIDGET section names the widget it draws.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ADashboardWidgetNamesItsWidget(string? widgetCode) =>
        Assert.Equal(new FieldIssue("visuals[0].widgetCode", FieldIssue.Required), Issue(Widget(widgetCode)));

    /// <summary>Only a DASHBOARD_WIDGET section names a widget: not another section, nor a chart.</summary>
    [Fact]
    public void OnlyADashboardWidgetNamesAWidget()
    {
        Assert.Equal(new FieldIssue("visuals[0].widgetCode", FieldIssue.NotAllowed), Issue(Placed(PortfolioSection.DashboardWidgets, 0) with { WidgetCode = "OPEN_RISKS" }));
        Assert.Equal(new FieldIssue("visuals[0].widgetCode", FieldIssue.NotAllowed), Issue(Visual() with { WidgetCode = "OPEN_RISKS" }));
        Assert.Equal(new FieldIssue("visuals[0].widgetCode", FieldIssue.NotAllowed), Issue(Drawing() with { WidgetCode = "OPEN_RISKS" }));
    }

    /// <summary>A widget code is 1–50 characters of letters, digits, '_', '.' and '-'; only its shape is checked, not that the dashboard has it.</summary>
    [Theory]
    [InlineData("OPEN_RISKS_BY_RATING", null)]
    [InlineData("fin.budget-vs-actual_2", null)]
    [InlineData("a", null)]
    [InlineData("OPEN RISKS", FieldIssue.NotAllowed)]
    [InlineData("OPEN_RISKS\n", FieldIssue.NotAllowed)]
    [InlineData("مخاطر", FieldIssue.NotAllowed)]
    [InlineData("<script>", FieldIssue.NotAllowed)]
    public void AWidgetCodeHasTheDashboardsShape(string widgetCode, string? code) =>
        Assert.Equal(code is null ? null : new FieldIssue("visuals[0].widgetCode", code), Issue(Widget(Regex.Unescape(widgetCode))));

    [Fact]
    public void AWidgetCodeIsAtMostFiftyCharacters()
    {
        Assert.Null(Issue(Widget(new string('W', 50))));
        Assert.Equal(new FieldIssue("visuals[0].widgetCode", FieldIssue.OutsideRange), Issue(Widget(new string('W', 51))));
    }

    /// <summary>Several DASHBOARD_WIDGET sections, one per widget; a second naming the same widget is refused (codes compare exactly).</summary>
    [Fact]
    public void ADesignPlacesManyWidgetsEachOnce()
    {
        Assert.Null(Issue(Widget("OPEN_RISKS_BY_RATING"), Widget("PROJECTS_BY_LIFECYCLE_STATE", 4), Widget("FINANCIAL_SUMMARY", 8), Widget("open_risks_by_rating", 12)));
        Assert.Equal(
            new FieldIssue("visuals[2].widgetCode", FieldIssue.Duplicate),
            Issue(Widget("OPEN_RISKS_BY_RATING"), Widget("PROJECTS_BY_LIFECYCLE_STATE", 4), Widget("OPEN_RISKS_BY_RATING", 8)));
    }

    /// <summary>The whole-widgets section saved before 19 Nov 2026 stays valid, and still once per design, beside single widgets.</summary>
    [Fact]
    public void TheDashboardWidgetsSectionStaysValid()
    {
        Assert.Null(Issue(Placed(PortfolioSection.DashboardWidgets, 0), Widget("OPEN_RISKS_BY_RATING", 4)));
        Assert.Equal(
            new FieldIssue("visuals[1].section", FieldIssue.Duplicate), Issue(Placed(PortfolioSection.DashboardWidgets, 0), Placed(PortfolioSection.DashboardWidgets, 4)));
    }

    /// <summary>A DASHBOARD_WIDGET section is a section like the others: no measure, grouping or field values.</summary>
    [Fact]
    public void ADashboardWidgetDrawsNoFigureOfItsOwn()
    {
        Assert.Equal("visuals[0].section", Issue(Widget("OPEN_RISKS") with { Measures = [PortfolioMeasure.Projects] })?.Field);
        Assert.Equal("visuals[0].section", Issue(Widget("OPEN_RISKS") with { VisualType = PortfolioVisualType.Card })?.Field);
    }

    private static PortfolioVisualInput Hiding(PortfolioSection section, params string[] codes) => Placed(section, 0) with { HiddenColumns = codes };

    /// <summary>20 Nov 2026 table columns: each of the seven table sections may hide columns.</summary>
    [Theory]
    [InlineData(PortfolioSection.PortfolioTable)]
    [InlineData(PortfolioSection.InitiativeTable)]
    [InlineData(PortfolioSection.SectorTable)]
    [InlineData(PortfolioSection.StatusTable)]
    [InlineData(PortfolioSection.CurrentStageTable)]
    [InlineData(PortfolioSection.TopFollowUp)]
    [InlineData(PortfolioSection.EntityCompletionTable)]
    public void ATableSectionHidesColumns(PortfolioSection section) => Assert.Null(Issue(Hiding(section, "VALUE", "GAP")));

    /// <summary>Only a table section hides columns: not another section, a chart, a card or a container.</summary>
    [Theory]
    [InlineData(PortfolioSection.OnPlanCounts)]
    [InlineData(PortfolioSection.FollowUpReasons)]
    [InlineData(PortfolioSection.EntityCompletion)]
    [InlineData(PortfolioSection.ProjectLocations)]
    [InlineData(PortfolioSection.DashboardWidgets)]
    public void AnotherSectionHidesNoColumn(PortfolioSection section) =>
        Assert.Equal(new FieldIssue("visuals[0].hiddenColumns", FieldIssue.NotAllowed), Issue(Hiding(section, "VALUE")));

    [Fact]
    public void OnlyASectionHidesColumns()
    {
        Assert.Equal(new FieldIssue("visuals[0].hiddenColumns", FieldIssue.NotAllowed), Issue(Widget("OPEN_RISKS") with { HiddenColumns = ["VALUE"] }));
        Assert.Equal(new FieldIssue("visuals[0].hiddenColumns", FieldIssue.NotAllowed), Issue(SmallCard(0, 0) with { HiddenColumns = ["VALUE"] }));
        Assert.Equal(new FieldIssue("visuals[0].hiddenColumns", FieldIssue.NotAllowed), Issue(Visual(PortfolioVisualType.Table) with { HiddenColumns = ["VALUE"] }));
        Assert.Equal(new FieldIssue("visuals[0].hiddenColumns", FieldIssue.NotAllowed), Issue(Container() with { HiddenColumns = ["VALUE"] }));
        Assert.Equal(
            new FieldIssue("visuals[0].children[0].hiddenColumns", FieldIssue.NotAllowed), Issue(Container(children: [SmallCard(0, 0) with { HiddenColumns = ["VALUE"] }])));
    }

    /// <summary>An empty list hides nothing, so it is taken on any visual, as null is.</summary>
    [Fact]
    public void AnEmptyListHidesNothingAnywhere()
    {
        Assert.Null(Issue(SmallCard(0, 0) with { HiddenColumns = [] }));
        Assert.Null(Issue(Hiding(PortfolioSection.OnPlanCounts)));
    }

    /// <summary>A column code is 1–40 upper-case letters and '_'; only the shape is checked, so a code the table does not have is taken and hides nothing.</summary>
    [Theory]
    [InlineData("VALUE", null)]
    [InlineData("NEEDING_FOLLOW_UP", null)]
    [InlineData("NOT_A_COLUMN_OF_THIS_TABLE", null)]
    [InlineData("value", FieldIssue.NotAllowed)]
    [InlineData("PLANNED SPEND", FieldIssue.NotAllowed)]
    [InlineData("SPLIT2", FieldIssue.NotAllowed)]
    [InlineData("VALUE\n", FieldIssue.NotAllowed)]
    [InlineData("", FieldIssue.NotAllowed)]
    public void AColumnCodeHasItsShape(string code, string? issue) =>
        Assert.Equal(issue is null ? null : new FieldIssue("visuals[0].hiddenColumns", issue), Issue(Hiding(PortfolioSection.SectorTable, Regex.Unescape(code))));

    [Fact]
    public void AColumnCodeIsAtMostFortyCharacters()
    {
        Assert.Null(Issue(Hiding(PortfolioSection.SectorTable, new string('C', 40))));
        Assert.Equal(new FieldIssue("visuals[0].hiddenColumns", FieldIssue.NotAllowed), Issue(Hiding(PortfolioSection.SectorTable, new string('C', 41))));
    }

    [Fact]
    public void ATableHidesAtMostTwentyColumns()
    {
        string[] codes = [.. Enumerable.Range(0, 21).Select(i => new string((char)('A' + i), 3))];
        Assert.Null(Issue(Hiding(PortfolioSection.SectorTable, codes[..20])));
        Assert.Equal(new FieldIssue("visuals[0].hiddenColumns", FieldIssue.OutsideRange), Issue(Hiding(PortfolioSection.SectorTable, codes)));
    }

    [Fact]
    public void AColumnIsHiddenOnce() =>
        Assert.Equal(new FieldIssue("visuals[0].hiddenColumns", FieldIssue.Duplicate), Issue(Hiding(PortfolioSection.SectorTable, "VALUE", "GAP", "VALUE")));

    /// <summary>The hidden columns are echoed as sent; an empty list as null, the same as a section hiding none.</summary>
    [Fact]
    public void TheHiddenColumnsAreEchoed()
    {
        IReadOnlyDictionary<string, PortfolioFieldType> types = new Dictionary<string, PortfolioFieldType>(StringComparer.Ordinal);
        Assert.Equal(["VALUE", "GAP"], PortfolioDesignService.ResultOf(Hiding(PortfolioSection.SectorTable, "VALUE", "GAP"), _ => [], types).HiddenColumns);
        Assert.Null(PortfolioDesignService.ResultOf(Hiding(PortfolioSection.SectorTable), _ => [], types).HiddenColumns);
        Assert.Null(PortfolioDesignService.ResultOf(Placed(PortfolioSection.SectorTable, 0), _ => [], types).HiddenColumns);
    }

    /// <summary>A saved row's hidden columns read back on its visual.</summary>
    [Fact]
    public void SavedHiddenColumnsReadBack()
    {
        PortfolioVisual sector = Saved(1, PortfolioVisualType.Section, "By sector");
        sector.Section = PortfolioSection.SectorTable;
        sector.HiddenColumns = ["VALUE", "GAP"];
        Assert.Equal(["VALUE", "GAP"], PortfolioDesignService.Nested([sector, Saved(2, PortfolioVisualType.Card, "Card", row: 8)])[0].HiddenColumns);
        Assert.Null(PortfolioDesignService.Nested([Saved(1, PortfolioVisualType.Card, "Card")])[0].HiddenColumns);
    }

    /// <summary>A grouping is read under the insights permission that shows that breakdown today, so the designer shows no one more than before.</summary>
    [Theory]
    [InlineData(PortfolioGroupBy.None, PermissionCatalogue.PortfolioSummaryView)]
    [InlineData(PortfolioGroupBy.Portfolio, PermissionCatalogue.PortfolioSummaryView)]
    [InlineData(PortfolioGroupBy.Sector, PermissionCatalogue.PortfolioBreakdownView)]
    [InlineData(PortfolioGroupBy.Entity, PermissionCatalogue.PortfolioBreakdownView)]
    [InlineData(PortfolioGroupBy.Stage, PermissionCatalogue.PortfolioStatusView)]
    [InlineData(PortfolioGroupBy.OnPlan, PermissionCatalogue.PortfolioStatusView)]
    public void AGroupingIsReadUnderItsInsightsPermission(PortfolioGroupBy groupBy, string permission) =>
        Assert.Equal(permission, PortfolioDesignService.PermissionOf(groupBy));

    /// <summary>
    /// Every field of the register's projections about projects is offered (18 Nov 2026 more fields: yes/no and dates too, and the snapshot history), each keyed
    /// by its projection and field, with its type's aggregations and its grain; the project-grain fields come first.
    /// </summary>
    [Fact]
    public void TheCatalogueOffersEveryFieldAboutProjects()
    {
        Assert.All(Catalogue, f => Assert.Matches(new Regex("^[A-Z][A-Z_]*\\.[A-Z][A-Z_]*\\.[A-Z][A-Z0-9_]*$"), f.Key));
        Assert.Equal(DashboardProjections.Reportable.Sum(p => p.Fields.Count), Catalogue.Count);
        Assert.DoesNotContain(Catalogue, f => f.ProjectionCode == DashboardProjections.DefinitionBacklog);
        Assert.Equal(PortfolioFieldType.Boolean, Assert.Single(Catalogue, f => f.FieldCode == "ACTUAL_PERCENT_OVERRIDDEN").Type);
        Assert.Equal(Catalogue.OrderBy(f => f.Grain).Select(f => f.Key), Catalogue.Select(f => f.Key));
        PortfolioFieldDescriptor spend = Assert.Single(Catalogue, f => f.Key == Spend);
        Assert.Equal((PortfolioFieldType.Sar, true, "WF-14"), (spend.Type, spend.IsSensitive, spend.SourceDomain));
        Assert.Equal([PortfolioFieldAggregation.Count], Assert.Single(Catalogue, f => f.Key == Health).Aggregations);
        Assert.DoesNotContain(PortfolioFieldAggregation.Sum, Assert.Single(Catalogue, f => f.Key == ActualPercent).Aggregations);
    }

    [Fact]
    public void AVisualDrawsFieldValues() => Assert.Null(Issue(Drawing()));

    /// <summary>Rule 1: the fixed measures or the field values, never both; a section neither.</summary>
    [Fact]
    public void AVisualDrawsMeasuresOrValuesNeverBoth()
    {
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.NotAllowed), Issue(Drawing() with { Measures = [PortfolioMeasure.Projects] }));
        Assert.Equal("visuals[0].section", Issue(Placed(PortfolioSection.PortfolioTable, 0) with { Values = [Value(Spend, PortfolioFieldAggregation.Sum)] })?.Field);
        Assert.Equal("visuals[0].section", Issue(Placed(PortfolioSection.PortfolioTable, 0) with { GroupField = Health })?.Field);
    }

    /// <summary>Rule 2: as many values as the type draws, a table up to six; each a field offered with an aggregation its type allows, no two alike.</summary>
    [Fact]
    public void ValuesAreCatalogueFieldsInTheTypesNumber()
    {
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.OutsideRange),
            Issue(Drawing(values: [Value(Spend, PortfolioFieldAggregation.Sum), Value(Spend, PortfolioFieldAggregation.Max)])));
        PortfolioFieldValue[] six = [.. new[] { PortfolioFieldAggregation.Count, PortfolioFieldAggregation.Sum, PortfolioFieldAggregation.Average, PortfolioFieldAggregation.Min,
            PortfolioFieldAggregation.Max }.Select(a => Value(Spend, a)), Value(OpenRisks, PortfolioFieldAggregation.Sum)];
        Assert.Null(Issue(Drawing(PortfolioVisualType.Table, values: six)));
        Assert.Equal(FieldIssue.OutsideRange, Issue(Drawing(PortfolioVisualType.Table, values: [.. six, Value(OpenRisks, PortfolioFieldAggregation.Max)]))?.Code);
        Assert.Equal(FieldIssue.Duplicate, Issue(Drawing(PortfolioVisualType.Column, values: [Value(Spend, PortfolioFieldAggregation.Sum), Value(Spend, PortfolioFieldAggregation.Sum)]))?.Code);
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(values: Value("PROJECT.PROJECT.BUDGET", PortfolioFieldAggregation.Sum)))?.Code);
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(values: Value("PROGRESS.PUBLISHED_PROGRESS_HISTORY.ACTUAL_PERCENT", PortfolioFieldAggregation.Average)))?.Code);
    }

    /// <summary>A sum of percentages means nothing, and a state is only counted.</summary>
    [Theory]
    [InlineData(ActualPercent, PortfolioFieldAggregation.Sum)]
    [InlineData(Health, PortfolioFieldAggregation.Average)]
    [InlineData(Health, PortfolioFieldAggregation.Max)]
    public void AnAggregationFitsItsFieldsType(string field, PortfolioFieldAggregation aggregation) =>
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.NotAllowed), Issue(Drawing(values: Value(field, aggregation))));

    /// <summary>Rule 3: a gauge draws a percentage's average, least or greatest; a pie and a donut a count or a sum.</summary>
    [Fact]
    public void AGaugeAndAPieDrawWhatTheyCanShow()
    {
        Assert.Null(Issue(Drawing(PortfolioVisualType.Gauge, groupField: null, values: Value(ActualPercent, PortfolioFieldAggregation.Average))));
        Assert.Equal("visuals[0].values", Issue(Drawing(PortfolioVisualType.Gauge, groupField: null, values: Value(Spend, PortfolioFieldAggregation.Max)))?.Field);
        Assert.Equal("visuals[0].values", Issue(Drawing(PortfolioVisualType.Gauge, groupField: null, values: Value(ActualPercent, PortfolioFieldAggregation.Count)))?.Field);
        Assert.Null(Issue(Drawing(PortfolioVisualType.Donut, values: Value(Health, PortfolioFieldAggregation.Count))));
        Assert.Equal("visuals[0].values", Issue(Drawing(PortfolioVisualType.Pie, values: Value(Spend, PortfolioFieldAggregation.Average)))?.Field);
    }

    /// <summary>Rule 4: a chart is grouped by a project attribute or a CODE field, never both; a card and a gauge by neither.</summary>
    [Fact]
    public void AGroupFieldIsACodeFieldInPlaceOfGroupBy()
    {
        Assert.Null(Issue(Drawing(groupBy: PortfolioGroupBy.Sector, groupField: null)));
        Assert.Null(Issue(Visual(groupBy: PortfolioGroupBy.None) with { GroupField = Health }));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Drawing(groupBy: PortfolioGroupBy.Sector)));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Drawing(groupField: Spend)));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Drawing(PortfolioVisualType.Card)));
        Assert.Equal(new FieldIssue("visuals[0].groupBy", FieldIssue.NotAllowed), Issue(Drawing(groupField: null)));
        Assert.Null(Issue(Drawing(PortfolioVisualType.Card, groupField: null)));
    }

    /// <summary>Rule 5: up to five filters, on a CODE field or a dimension, with one to twenty values of the right shape.</summary>
    [Fact]
    public void FiltersTakeCodeFieldsAndDimensions()
    {
        PortfolioFieldFilter active = new("PROJECT.STATUS", ["ACTIVE"]);
        Assert.Null(Issue(Drawing(filters: [active, new(Health, ["RED", "AMBER"]), new("PROJECT.SECTOR", [Guid.NewGuid().ToString()])])));
        Assert.Equal(FieldIssue.OutsideRange, Issue(Drawing(filters: [active, active, active, active, active, active]))?.Code);
        Assert.Equal(FieldIssue.OutsideRange, Issue(Drawing(filters: [new("PROJECT.STATUS", [.. Enumerable.Range(0, 21).Select(i => $"S{i}")])]))?.Code);
        Assert.Equal(FieldIssue.Required, Issue(Drawing(filters: [new(Health, [])]))?.Code);
        Assert.Equal(new FieldIssue("visuals[0].filters", FieldIssue.NotAllowed), Issue(Drawing(filters: [new("FINANCIAL_KPI.KPI_CONDITION.RAG_RED", ["1"])])));
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(filters: [new("PROJECT.NONE", ["X"])]))?.Code);
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(filters: [new("PROJECT.SECTOR", ["ACTIVE"])]))?.Code);
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(filters: [new(Health, ["red"])]))?.Code);
    }

    /// <summary>The arithmetic over the revealed cells only: a count is 0 with none, any other aggregation null ("not available", never 0).</summary>
    [Theory]
    [InlineData(PortfolioFieldAggregation.Count, "3")]
    [InlineData(PortfolioFieldAggregation.Sum, "350.50")]
    [InlineData(PortfolioFieldAggregation.Average, "116.83")]
    [InlineData(PortfolioFieldAggregation.Min, "0.25")]
    [InlineData(PortfolioFieldAggregation.Max, "250.00")]
    public void AnAggregationCountsTheRevealedCellsOnly(PortfolioFieldAggregation aggregation, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            PortfolioDesignService.Aggregate(aggregation, PortfolioFieldType.Sar, ["100.25", null, "250.00", "0.25", null]));

    [Theory]
    [InlineData(PortfolioFieldAggregation.Sum)]
    [InlineData(PortfolioFieldAggregation.Average)]
    [InlineData(PortfolioFieldAggregation.Min)]
    [InlineData(PortfolioFieldAggregation.Max)]
    public void NothingRevealedIsNotAvailable(PortfolioFieldAggregation aggregation)
    {
        Assert.Null(PortfolioDesignService.Aggregate(aggregation, PortfolioFieldType.Sar, [null, null]));
        Assert.Equal(0m, PortfolioDesignService.Aggregate(PortfolioFieldAggregation.Count, PortfolioFieldType.Sar, [null, null]));
    }

    [Fact]
    public void AStateIsCounted() => Assert.Equal(2m, PortfolioDesignService.Aggregate(PortfolioFieldAggregation.Count, PortfolioFieldType.Code, ["RED", "GREEN", null]));

    [Fact]
    public void EveryGroupingIsAFilterDimension() =>
        Assert.Equal(Enum.GetValues<PortfolioGroupBy>().Length - 1, PortfolioDesignService.Dimensions.Count);

    /// <summary>A viewer formats each value by its field's type without reading the catalogue; fixed measures carry none.</summary>
    [Fact]
    public void EachValueCarriesItsFieldsType()
    {
        Dictionary<string, PortfolioFieldType> types = Catalogue.ToDictionary(f => f.Key, f => f.Type, StringComparer.Ordinal);

        Assert.Equal(
            [PortfolioFieldType.Sar, PortfolioFieldType.Percent, PortfolioFieldType.Code],
            PortfolioDesignService.ValueTypesOf(
                Drawing(PortfolioVisualType.Table, values: [Value(Spend, PortfolioFieldAggregation.Sum), Value(ActualPercent, PortfolioFieldAggregation.Average), Value(Health, PortfolioFieldAggregation.Count)]),
                types));
        Assert.Null(PortfolioDesignService.ValueTypesOf(Visual(), types));
    }

    // The 18 Nov 2026 more fields: PROJECT.PROJECT_PROFILE, yes/no and date fields, snapshot-grain fields and the LINE visual.
    private const string Budget = "PROJECT.PROJECT_PROFILE.REGISTRATION_BUDGET";
    private const string Strategic = "PROJECT.PROJECT_PROFILE.IS_STRATEGIC";
    private const string PlannedEnd = "PROJECT.PROJECT_PROFILE.PLANNED_END_DATE";
    private const string ActivatedAt = "PROJECT.PROJECT_PROFILE.ACTIVATED_AT";
    private const string Funding = "PROJECT.PROJECT_PROFILE.FUNDING_SOURCE";
    private const string PeriodEnd = "PROGRESS.PUBLISHED_PROGRESS_HISTORY.PERIOD_END";
    private const string PublishedAt = "PROGRESS.PUBLISHED_PROGRESS_HISTORY.PUBLISHED_AT";
    private const string HistoryActual = "PROGRESS.PUBLISHED_PROGRESS_HISTORY.ACTUAL_PERCENT";
    private const string HistoryHealth = "PROGRESS.PUBLISHED_PROGRESS_HISTORY.OVERALL_HEALTH";

    private static readonly Dictionary<string, PortfolioFieldDescriptor> ByKey = Catalogue.ToDictionary(f => f.Key, StringComparer.Ordinal);

    /// <summary>A visual of field values grouped by a field (no project attribute), with a date unit when the field is a date.</summary>
    private static PortfolioVisualInput ByField(
        PortfolioVisualType type, string? groupField, PortfolioDateUnit? unit, IReadOnlyList<PortfolioFieldFilter>? filters = null, params PortfolioFieldValue[] values) =>
        new(new BilingualLabel("عبر الزمن", "Over time"), type, [], PortfolioGroupBy.None, 0, 0, 6, 5, null, values, groupField, filters, unit);

    private static PortfolioVisualInput Line(string? groupField = PeriodEnd, PortfolioDateUnit? unit = PortfolioDateUnit.Month, params PortfolioFieldValue[] values) =>
        ByField(PortfolioVisualType.Line, groupField, unit, null, values.Length == 0 ? [Value(HistoryActual, PortfolioFieldAggregation.Average)] : values);

    [Fact]
    public void TheProjectProfileOffersItsSixteenFieldsOfProjects()
    {
        List<PortfolioFieldDescriptor> profile = [.. Catalogue.Where(f => f.ProjectionCode == DashboardProjections.ProjectProfile)];

        Assert.Equal(16, profile.Count);
        Assert.All(profile, f => Assert.Equal((PortfolioFieldGrain.Project, "WF-01"), (f.Grain, f.SourceDomain)));
        Assert.Equal((PortfolioFieldType.Sar, true), (ByKey[Budget].Type, ByKey[Budget].IsSensitive));
        Assert.Equal(["STRATEGY_BUDGET_FULL", "STRATEGY_BUDGET_PARTIAL", "NOT_STRATEGY_BUDGET"], ByKey[Funding].Values);
        Assert.Equal(["SCHEDULE", "STAGES"], ByKey["PROJECT.PROJECT_PROFILE.PROGRESS_METHOD"].Values);
        Assert.Equal(
            [PortfolioFieldType.Sar, .. Enumerable.Repeat(PortfolioFieldType.Percent, 5), .. Enumerable.Repeat(PortfolioFieldType.Code, 3), PortfolioFieldType.Boolean,
                PortfolioFieldType.Boolean, PortfolioFieldType.Date, PortfolioFieldType.Date, PortfolioFieldType.Date, PortfolioFieldType.DateTime, PortfolioFieldType.DateTime],
            profile.Select(f => f.Type));
        Assert.Equal([PortfolioFieldAggregation.Count, PortfolioFieldAggregation.CountTrue, PortfolioFieldAggregation.ShareTrue], ByKey[Strategic].Aggregations);
        Assert.Equal([PortfolioFieldAggregation.Count, PortfolioFieldAggregation.Min, PortfolioFieldAggregation.Max], ByKey[ActivatedAt].Aggregations);
    }

    [Fact]
    public void ThePublishedHistoryIsOfferedAtSnapshotGrain()
    {
        List<PortfolioFieldDescriptor> history = [.. Catalogue.Where(f => f.ProjectionCode == DashboardProjections.PublishedProgressHistory)];

        Assert.Equal(["PUBLISHED_AT", "PERIOD_START", "PERIOD_END", "OVERALL_HEALTH", "ACTUAL_PERCENT", "PLANNED_PERCENT"], history.Select(f => f.FieldCode));
        Assert.All(history, f => Assert.Equal(PortfolioFieldGrain.Snapshot, f.Grain));
        Assert.Equal(PortfolioFieldType.DateTime, ByKey[PublishedAt].Type);
        Assert.All(Catalogue.Where(f => f.ProjectionCode != DashboardProjections.PublishedProgressHistory), f => Assert.Equal(PortfolioFieldGrain.Project, f.Grain));
    }

    [Theory]
    [InlineData(PortfolioFieldAggregation.Count, "4")]
    [InlineData(PortfolioFieldAggregation.CountTrue, "3")]
    [InlineData(PortfolioFieldAggregation.ShareTrue, "75")]
    public void AYesNoIsCountedAndItsYesShareTaken(PortfolioFieldAggregation aggregation, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            PortfolioDesignService.Aggregate(aggregation, PortfolioFieldType.Boolean, ["true", "false", null, "true", "true"]));

    [Fact]
    public void AYesShareHasTwoPlacesAndIsNotAvailableWithNothingRevealed()
    {
        Assert.Equal(33.33m, PortfolioDesignService.Aggregate(PortfolioFieldAggregation.ShareTrue, PortfolioFieldType.Boolean, ["true", "false", "false"]));
        Assert.Null(PortfolioDesignService.Aggregate(PortfolioFieldAggregation.ShareTrue, PortfolioFieldType.Boolean, [null]));
        Assert.Equal(0m, PortfolioDesignService.Aggregate(PortfolioFieldAggregation.CountTrue, PortfolioFieldType.Boolean, [null]));
    }

    /// <summary>A date's earliest and latest are day numbers since 1970-01-01; a moment counts by its UTC date.</summary>
    [Fact]
    public void ADatesEarliestAndLatestAreDayNumbers()
    {
        static decimal Day(int year, int month, int day) => new DateOnly(year, month, day).DayNumber - new DateOnly(1970, 1, 1).DayNumber;

        Assert.Equal(Day(2025, 12, 31), PortfolioDesignService.Aggregate(PortfolioFieldAggregation.Min, PortfolioFieldType.Date, ["2026-03-01", null, "2025-12-31"]));
        Assert.Equal(Day(2026, 3, 1), PortfolioDesignService.Aggregate(PortfolioFieldAggregation.Max, PortfolioFieldType.Date, ["2026-03-01", null, "2025-12-31"]));
        Assert.Equal(Day(2026, 11, 18), PortfolioDesignService.Aggregate(PortfolioFieldAggregation.Max, PortfolioFieldType.DateTime, ["2026-11-18T23:59:00Z", "2026-01-02T00:00:00Z"]));
        Assert.Equal(0m, PortfolioDesignService.Aggregate(PortfolioFieldAggregation.Min, PortfolioFieldType.Date, ["1970-01-01"]));
        Assert.Null(PortfolioDesignService.Aggregate(PortfolioFieldAggregation.Min, PortfolioFieldType.Date, [null]));
    }

    [Theory]
    [InlineData(Strategic, PortfolioFieldAggregation.Sum)]
    [InlineData(Strategic, PortfolioFieldAggregation.Average)]
    [InlineData(PlannedEnd, PortfolioFieldAggregation.Sum)]
    [InlineData(PlannedEnd, PortfolioFieldAggregation.Average)]
    [InlineData(OpenRisks, PortfolioFieldAggregation.ShareTrue)]
    public void YesNoAndDateAggregationsFitTheirTypes(string field, PortfolioFieldAggregation aggregation) =>
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.NotAllowed), Issue(Drawing(values: Value(field, aggregation))));

    /// <summary>Rule 3 widened: a gauge also draws a yes share; a pie and a donut also a count of yeses.</summary>
    [Fact]
    public void AGaugeDrawsAYesShareAndAPieACountOfYeses()
    {
        Assert.Null(Issue(Drawing(PortfolioVisualType.Gauge, groupField: null, values: Value(Strategic, PortfolioFieldAggregation.ShareTrue))));
        Assert.Equal("visuals[0].values", Issue(Drawing(PortfolioVisualType.Gauge, groupField: null, values: Value(Strategic, PortfolioFieldAggregation.CountTrue)))?.Field);
        Assert.Null(Issue(Drawing(PortfolioVisualType.Pie, values: Value(Strategic, PortfolioFieldAggregation.CountTrue))));
        Assert.Equal("visuals[0].values", Issue(Drawing(PortfolioVisualType.Pie, values: Value(Strategic, PortfolioFieldAggregation.ShareTrue)))?.Field);
    }

    /// <summary>A visual groups by a yes/no with no unit, and by a date with its year or month, which a date requires and anything else refuses.</summary>
    [Fact]
    public void ADateGroupNamesItsUnit()
    {
        Assert.Null(Issue(Drawing(groupField: Strategic)));
        Assert.Null(Issue(ByField(PortfolioVisualType.Column, PlannedEnd, PortfolioDateUnit.Year, null, Value(Budget, PortfolioFieldAggregation.Sum))));
        Assert.Null(Issue(ByField(PortfolioVisualType.Column, ActivatedAt, PortfolioDateUnit.Month, null, Value(Budget, PortfolioFieldAggregation.Sum))));
        Assert.Equal(new FieldIssue("visuals[0].groupDateUnit", FieldIssue.Required),
            Issue(ByField(PortfolioVisualType.Column, PlannedEnd, null, null, Value(Budget, PortfolioFieldAggregation.Sum))));
        Assert.Equal(new FieldIssue("visuals[0].groupDateUnit", FieldIssue.NotAllowed),
            Issue(ByField(PortfolioVisualType.Column, Health, PortfolioDateUnit.Year, null, Value(Budget, PortfolioFieldAggregation.Sum))));
        Assert.Equal(new FieldIssue("visuals[0].groupDateUnit", FieldIssue.NotAllowed),
            Issue(ByField(PortfolioVisualType.Column, PlannedEnd, (PortfolioDateUnit)9, null, Value(Budget, PortfolioFieldAggregation.Sum))));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Drawing(groupField: Budget)));
        Assert.Equal("visuals[0].section", Issue(Placed(PortfolioSection.PortfolioTable, 0) with { GroupDateUnit = PortfolioDateUnit.Year })?.Field);
    }

    /// <summary>Rule 5 widened: a yes/no filters by TRUE or FALSE, a date by year.</summary>
    [Fact]
    public void YesNoAndDateFieldsFilterByYesNoAndYear()
    {
        Assert.Null(Issue(Drawing(filters: [new(Strategic, ["TRUE"]), new(PlannedEnd, ["2026", "2027"]), new(ActivatedAt, ["2025"])])));
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(filters: [new(Strategic, ["true"])]))?.Code);
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(filters: [new(Strategic, ["YES"])]))?.Code);
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(filters: [new(PlannedEnd, ["26"])]))?.Code);
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(filters: [new(PlannedEnd, ["2026-03"])]))?.Code);
        Assert.Equal(FieldIssue.NotAllowed, Issue(Drawing(filters: [new(Budget, ["1"])]))?.Code);
    }

    /// <summary>A line draws one to three field values, never fixed measures, and is grouped by a field only.</summary>
    [Fact]
    public void ALineDrawsFieldValuesGroupedByAField()
    {
        Assert.Null(Issue(Line()));
        Assert.Null(Issue(Line(PlannedEnd, PortfolioDateUnit.Year, Value(Budget, PortfolioFieldAggregation.Sum), Value(OpenRisks, PortfolioFieldAggregation.Sum),
            Value(ActualPercent, PortfolioFieldAggregation.Average))));
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.OutsideRange), Issue(Line(values:
            [Value(HistoryActual, PortfolioFieldAggregation.Average), Value(HistoryActual, PortfolioFieldAggregation.Min), Value(HistoryActual, PortfolioFieldAggregation.Max),
                Value(HistoryActual, PortfolioFieldAggregation.Count)])));
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.Required), Issue(Visual(PortfolioVisualType.Line, PortfolioGroupBy.None, measures: []) with { Measures = [] }));
        Assert.Equal(new FieldIssue("visuals[0].measures", FieldIssue.NotAllowed), Issue(Visual(PortfolioVisualType.Line, PortfolioGroupBy.None)));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.Required), Issue(Line(groupField: null, unit: null)));
        Assert.Equal(new FieldIssue("visuals[0].groupBy", FieldIssue.NotAllowed),
            Issue(Line(groupField: null, unit: null) with { GroupBy = PortfolioGroupBy.Sector, Values = [Value(Budget, PortfolioFieldAggregation.Sum)] }));
    }

    /// <summary>Rule 7: one grain per visual; a snapshot visual is a period chart or by its own state, as a line, columns, bars or a table.</summary>
    [Fact]
    public void ASnapshotVisualIsOfOneGrainGroupedOverTime()
    {
        Assert.Null(Issue(ByField(PortfolioVisualType.Column, PublishedAt, PortfolioDateUnit.Year, null, Value(HistoryActual, PortfolioFieldAggregation.Max))));
        Assert.Null(Issue(ByField(PortfolioVisualType.Table, HistoryHealth, null, null, Value(HistoryActual, PortfolioFieldAggregation.Count))));
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.NotAllowed),
            Issue(Line(values: [Value(HistoryActual, PortfolioFieldAggregation.Average), Value(ActualPercent, PortfolioFieldAggregation.Average)])));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Line(PlannedEnd, PortfolioDateUnit.Year)));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed),
            Issue(Line(PeriodEnd, PortfolioDateUnit.Month, Value(ActualPercent, PortfolioFieldAggregation.Average))));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Visual(groupBy: PortfolioGroupBy.None) with { GroupField = HistoryHealth }));
        Assert.Equal(new FieldIssue("visuals[0].visualType", FieldIssue.NotAllowed),
            Issue(ByField(PortfolioVisualType.Pie, HistoryHealth, null, null, Value(HistoryActual, PortfolioFieldAggregation.Count))));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.Required),
            Issue(ByField(PortfolioVisualType.Table, null, null, null, Value(HistoryActual, PortfolioFieldAggregation.Average))));
    }

    /// <summary>A snapshot visual's filters narrow its projects (project fields, dimensions) or its snapshots (its own state); a project visual takes no snapshot filter.</summary>
    [Fact]
    public void ASnapshotFilterNarrowsTheSnapshotsOfASnapshotVisualOnly()
    {
        IReadOnlyList<PortfolioFieldFilter> filters = [new(HistoryHealth, ["RED"]), new(Strategic, ["TRUE"]), new("PROJECT.STATUS", ["ACTIVE"])];
        Assert.Null(Issue(ByField(PortfolioVisualType.Line, PeriodEnd, PortfolioDateUnit.Month, filters, Value(HistoryActual, PortfolioFieldAggregation.Average))));
        Assert.Equal(new FieldIssue("visuals[0].filters", FieldIssue.NotAllowed), Issue(Drawing(filters: [new(HistoryHealth, ["RED"])])));
        Assert.Equal(new FieldIssue("visuals[0].filters", FieldIssue.NotAllowed),
            Issue(ByField(PortfolioVisualType.Line, PeriodEnd, PortfolioDateUnit.Month, [new(PeriodEnd, ["2026"])], Value(HistoryActual, PortfolioFieldAggregation.Average))));
    }

    private static readonly OnPlanThresholds Thresholds = new(5m, 10m);

    private static PortfolioProjectRow Row(ProjectLifecycleState state = ProjectLifecycleState.Active) =>
        new(new PMPlatform.Domain.Project.Project { Id = Guid.NewGuid(), Title = new NarrativeText("مشروع", Language.Ar), LifecycleState = state }, ProjectIndicators.None);

    private static Dictionary<string, IReadOnlyList<PortfolioFieldCell>> Cells(params (string Key, string? Value)[] cells) =>
        cells.ToDictionary(c => c.Key, IReadOnlyList<PortfolioFieldCell> (c) => [new PortfolioFieldCell(c.Value)], StringComparer.Ordinal);

    private static IReadOnlyList<PortfolioVisualGroup> Groups(
        PortfolioVisualInput visual, IReadOnlyList<PortfolioProjectRow> rows, Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells) =>
        PortfolioDesignService.Groups(visual, rows, null, amounts: true, Thresholds, cells, ByKey);

    /// <summary>Date groups run in time order, whatever their size, the projects with no date last; a yes/no filter keeps the yeses.</summary>
    [Fact]
    public void DateGroupsRunInTimeOrderAndAYesNoFilterKeepsItsValue()
    {
        PortfolioProjectRow a = Row(), b = Row(), c = Row(), d = Row(), e = Row();
        Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells = new()
        {
            [a.Project.Id] = Cells((PlannedEnd, "2027-02-01"), (Strategic, "true")),
            [b.Project.Id] = Cells((PlannedEnd, "2026-12-31"), (Strategic, "true")),
            [c.Project.Id] = Cells((PlannedEnd, "2027-06-30"), (Strategic, "true")),
            [d.Project.Id] = Cells((PlannedEnd, null), (Strategic, "true")),
            [e.Project.Id] = Cells((PlannedEnd, "2025-01-01"), (Strategic, "false")),
        };

        IReadOnlyList<PortfolioVisualGroup> byYear = Groups(
            ByField(PortfolioVisualType.Column, PlannedEnd, PortfolioDateUnit.Year, [new(Strategic, ["TRUE"])], Value(PlannedEnd, PortfolioFieldAggregation.Count)), [a, b, c, d, e], cells);
        Assert.Equal(["2026", "2027", null], byYear.Select(g => g.Key));
        Assert.Equal([1, 2, 1], byYear.Select(g => g.Projects));
        Assert.Equal([1m, 2m, 0m], byYear.Select(g => g.Values[0]));

        IReadOnlyList<PortfolioVisualGroup> byMonth = Groups(
            ByField(PortfolioVisualType.Line, PlannedEnd, PortfolioDateUnit.Month, [new(PlannedEnd, ["2027"])], Value(Strategic, PortfolioFieldAggregation.ShareTrue)), [a, b, c, d, e], cells);
        Assert.Equal(["2027-02", "2027-06"], byMonth.Select(g => g.Key));
    }

    [Fact]
    public void AYesNoGroupsByTrueAndFalse()
    {
        PortfolioProjectRow a = Row(), b = Row(), c = Row();
        Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells = new()
        {
            [a.Project.Id] = Cells((Strategic, "true"), (Budget, "100.00")),
            [b.Project.Id] = Cells((Strategic, "true"), (Budget, null)),
            [c.Project.Id] = Cells((Strategic, "false"), (Budget, "50.00")),
        };

        IReadOnlyList<PortfolioVisualGroup> groups = Groups(Drawing(groupField: Strategic, values: Value(Budget, PortfolioFieldAggregation.Sum)), [a, b, c], cells);

        Assert.Equal(["TRUE", "FALSE"], groups.Select(g => g.Key));
        Assert.Equal([100m, 50m], groups.Select(g => g.Values[0]));
        Assert.Equal([1, 1], groups.Select(g => g.Measured));
    }

    /// <summary>
    /// A period chart: the population's snapshots grouped by their period, in time order; <c>Projects</c> are the distinct projects with a snapshot in the group,
    /// <c>Measured</c> the snapshots whose value is revealed; a snapshot filter narrows the snapshots, a project filter the projects.
    /// </summary>
    [Fact]
    public void SnapshotsGroupByTheirPeriod()
    {
        PortfolioProjectRow a = Row(), b = Row(), c = Row(ProjectLifecycleState.Suspended);
        static IReadOnlyList<PortfolioFieldCell> History(params string?[] values) => [.. values.Select((v, i) => new PortfolioFieldCell(v, i))];
        Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells = new()
        {
            [a.Project.Id] = new Dictionary<string, IReadOnlyList<PortfolioFieldCell>>(StringComparer.Ordinal)
            {
                [PeriodEnd] = History("2026-08-31", "2026-09-30", "2026-10-31"),
                [HistoryActual] = History("10", "20", "30"),
                [HistoryHealth] = History("GREEN", "RED", "RED"),
            },
            [b.Project.Id] = new Dictionary<string, IReadOnlyList<PortfolioFieldCell>>(StringComparer.Ordinal)
            {
                [PeriodEnd] = History("2026-09-30", "2026-10-31"),
                [HistoryActual] = History("40", null),
                [HistoryHealth] = History("RED", "RED"),
            },
            [c.Project.Id] = new Dictionary<string, IReadOnlyList<PortfolioFieldCell>>(StringComparer.Ordinal)
            {
                [PeriodEnd] = History("2026-09-30"),
                [HistoryActual] = History("90"),
                [HistoryHealth] = History("RED"),
            },
        };

        IReadOnlyList<PortfolioVisualGroup> all = Groups(Line(), [a, b, c], cells);
        Assert.Equal(["2026-08", "2026-09", "2026-10"], all.Select(g => g.Key));
        Assert.Equal([1, 3, 2], all.Select(g => g.Projects));
        Assert.Equal([1, 3, 1], all.Select(g => g.Measured));
        Assert.Equal([10m, 50m, 30m], all.Select(g => g.Values[0]));

        IReadOnlyList<PortfolioVisualGroup> red = Groups(
            ByField(PortfolioVisualType.Line, PeriodEnd, PortfolioDateUnit.Month, [new(HistoryHealth, ["RED"]), new("PROJECT.STATUS", ["ACTIVE"])],
                Value(HistoryActual, PortfolioFieldAggregation.Average)),
            [a, b, c], cells);
        Assert.Equal(["2026-09", "2026-10"], red.Select(g => g.Key));
        Assert.Equal([30m, 30m], red.Select(g => g.Values[0]));
        Assert.Equal([2, 2], red.Select(g => g.Projects));
    }

    private static PortfolioAxes Axes(
        BilingualLabel? categoryTitle = null, BilingualLabel? valueTitle = null, decimal? min = null, decimal? max = null, bool showCategory = true, bool gridlines = true) =>
        new(new PortfolioAxis(showCategory, categoryTitle), new PortfolioValueAxis(Show: true, valueTitle, min, max, gridlines));

    /// <summary>The 19 Nov 2026 axes: a bar, a column and a line take them, with titles, a range or neither.</summary>
    [Fact]
    public void ABarColumnOrLineTakesAxes()
    {
        PortfolioAxes axes = Axes(new BilingualLabel("المحفظة", "Portfolio"), new BilingualLabel("المشاريع", "Projects"), 0, 100, showCategory: false, gridlines: false);

        Assert.Null(Issue(Visual(PortfolioVisualType.Bar) with { Axes = axes }));
        Assert.Null(Issue(Visual(PortfolioVisualType.Column) with { Axes = axes }));
        Assert.Null(Issue(Line() with { Axes = axes }));
        Assert.Null(Issue(Visual(PortfolioVisualType.Column) with { Axes = PortfolioAxes.Default }));
    }

    /// <summary>Any other type, a section too, takes no axes.</summary>
    [Fact]
    public void OnlyABarColumnOrLineTakesAxes()
    {
        FieldIssue refused = new("visuals[0].axes", FieldIssue.NotAllowed);

        Assert.Equal(refused, Issue(Visual(PortfolioVisualType.Card, PortfolioGroupBy.None, width: 4, height: 2) with { Axes = PortfolioAxes.Default }));
        Assert.Equal(refused, Issue(Visual(PortfolioVisualType.Pie) with { Axes = PortfolioAxes.Default }));
        Assert.Equal(refused, Issue(Visual(PortfolioVisualType.Donut) with { Axes = PortfolioAxes.Default }));
        Assert.Equal(refused, Issue(Visual(PortfolioVisualType.Table) with { Axes = PortfolioAxes.Default }));
        Assert.Equal(refused, Issue(Visual(PortfolioVisualType.Gauge, PortfolioGroupBy.None, measures: PortfolioMeasure.AverageActual) with { Axes = PortfolioAxes.Default }));
        Assert.Equal(refused, Issue(Placed(PortfolioSection.PortfolioTable, 0) with { Axes = PortfolioAxes.Default }));
    }

    /// <summary>Both axes are named: a design read from a body that left one out is refused, not drawn with a guess.</summary>
    [Fact]
    public void AxesNameBothAxes()
    {
        Assert.Equal(new FieldIssue("visuals[0].axes.category", FieldIssue.Required), Issue(Visual() with { Axes = PortfolioAxes.Default with { Category = null! } }));
        Assert.Equal(new FieldIssue("visuals[0].axes.value", FieldIssue.Required), Issue(Visual() with { Axes = PortfolioAxes.Default with { Value = null! } }));
    }

    /// <summary>A title is one to sixty characters in each language once trimmed.</summary>
    [Fact]
    public void AnAxisTitleIsAtMostSixtyCharacters()
    {
        string sixty = new('a', 60);
        string sixtyOne = new('a', 61);

        Assert.Null(Issue(Visual() with { Axes = Axes(new BilingualLabel($"  {new string('ب', 60)}  ", $" {sixty} "), new BilingualLabel("ب", "b")) }));
        Assert.Equal(new FieldIssue("visuals[0].axes.category.title", FieldIssue.OutsideRange),
            Issue(Visual() with { Axes = Axes(new BilingualLabel("المحفظة", sixtyOne)) }));
        Assert.Equal(new FieldIssue("visuals[0].axes.value.title", FieldIssue.OutsideRange),
            Issue(Visual() with { Axes = Axes(valueTitle: new BilingualLabel(new string('ب', 61), "Projects")) }));
    }

    /// <summary>The value axis starts below where it ends; either alone is fine, the other then automatic.</summary>
    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 5)]
    public void TheValueAxisStartsBelowItsEnd(int min, int max) =>
        Assert.Equal(new FieldIssue("visuals[0].axes.value.max", FieldIssue.OutsideRange), Issue(Visual() with { Axes = Axes(min: min, max: max) }));

    [Fact]
    public void TheValueAxisMayFixOnlyItsStartOrItsEnd()
    {
        Assert.Null(Issue(Visual() with { Axes = Axes(min: -20) }));
        Assert.Null(Issue(Visual() with { Axes = Axes(max: 0.5m) }));
        Assert.Null(Issue(Visual() with { Axes = Axes(min: -1_000_000_000_000m, max: 1_000_000_000_000m) }));
    }

    /// <summary>A start and an end lie within ±10¹².</summary>
    [Fact]
    public void TheValueAxisRangeStaysWithinATrillion()
    {
        Assert.Equal(new FieldIssue("visuals[0].axes.value.min", FieldIssue.OutsideRange), Issue(Visual() with { Axes = Axes(min: -1_000_000_000_001m) }));
        Assert.Equal(new FieldIssue("visuals[0].axes.value.max", FieldIssue.OutsideRange), Issue(Visual() with { Axes = Axes(max: 1_000_000_000_001m) }));
    }

    /// <summary>What is stored: nothing for the defaults or a type without axes; otherwise the API's camelCase shape with the titles trimmed.</summary>
    [Fact]
    public void AxesAreStoredOnlyWhenTheyDifferFromTheDefaults()
    {
        Assert.Null(PortfolioDesignService.AxesOf(Visual()));
        Assert.Null(PortfolioDesignService.AxesOf(Visual() with { Axes = PortfolioAxes.Default }));
        Assert.Null(PortfolioDesignService.AxesOf(Visual(PortfolioVisualType.Pie) with { Axes = Axes(min: 0) }));

        // Compared as JSON: the serialiser escapes the Arabic, and jsonb stores it unescaped.
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"category":{"show":true,"title":{"ar":"المحفظة","en":"Portfolio"}},"value":{"show":true,"title":null,"min":0,"max":100,"gridlines":false}}"""),
            JsonNode.Parse(PortfolioDesignService.AxesOf(Visual() with { Axes = Axes(new BilingualLabel(" المحفظة ", "Portfolio  "), min: 0, max: 100, gridlines: false) })!)));
    }

    private static PortfolioVisualInput Container(decimal column = 0, decimal row = 0, decimal width = 12, decimal height = 6, params PortfolioVisualInput[] children) =>
        new(new BilingualLabel("حاوية", "Container"), PortfolioVisualType.Container, [], PortfolioGroupBy.None, column, row, width, height, Children: children);

    private static PortfolioVisualInput SmallCard(decimal column, decimal row, decimal width = 4, decimal height = 2) =>
        Visual(PortfolioVisualType.Card, PortfolioGroupBy.None, column, row, width, height);

    /// <summary>19 Nov 2026 containers: a container holds visuals on its inner grid, its children's places counted from its own first column and row.</summary>
    [Fact]
    public void AContainerHoldsVisualsOnItsInnerGrid()
    {
        Assert.Null(Issue(Container(children: [SmallCard(0, 0), Visual(column: 4, row: 0, width: 8, height: 5)]), Visual(row: 6)));
        Assert.Null(Issue(Container(column: 6, width: 6, height: 2, children: [SmallCard(0, 0, width: 6, height: 1)]), Visual(width: 6, height: 2)));
        Assert.Null(Issue(Container(children: [])));
        Assert.Null(Issue(Container() with { Measures = [], Values = [], Filters = [] }));
    }

    /// <summary>Rule 1: only a container has children, and a child is never a container (one level only).</summary>
    [Fact]
    public void OnlyAContainerHoldsChildrenAndNeverAContainer()
    {
        Assert.Equal(new FieldIssue("visuals[0].children", FieldIssue.NotAllowed), Issue(Visual() with { Children = [SmallCard(0, 0)] }));
        Assert.Equal(new FieldIssue("visuals[0].children", FieldIssue.NotAllowed), Issue(Visual() with { Children = [] }));
        Assert.Equal(new FieldIssue("visuals[0].children[1].visualType", FieldIssue.NotAllowed),
            Issue(Container(children: [SmallCard(0, 0), Container(column: 4, width: 4, height: 3)])));
        Assert.Equal(new FieldIssue("visuals[0].children[0].children", FieldIssue.NotAllowed),
            Issue(Container(children: [SmallCard(0, 0) with { Children = [] }])));
    }

    public static TheoryData<string, PortfolioVisualInput> ContainersDrawingSomething => new()
    {
        { "measures", Container() with { Measures = [PortfolioMeasure.Projects] } },
        { "values", Container() with { Values = [new PortfolioFieldValue(Spend, PortfolioFieldAggregation.Sum)] } },
        { "groupBy", Container() with { GroupBy = PortfolioGroupBy.Sector } },
        { "groupField", Container() with { GroupField = Health } },
        { "groupDateUnit", Container() with { GroupDateUnit = PortfolioDateUnit.Year } },
        { "filters", Container() with { Filters = [new PortfolioFieldFilter("PROJECT.STATUS", ["ACTIVE"])] } },
        { "axes", Container() with { Axes = PortfolioAxes.Default } },
        { "section", Container() with { Section = PortfolioSection.PortfolioTable } },
        { "widgetCode", Container() with { WidgetCode = "OPEN_RISKS" } },
    };

    /// <summary>Rule 2: a container draws nothing of its own.</summary>
    [Theory]
    [MemberData(nameof(ContainersDrawingSomething))]
    public void AContainerDrawsNothingOfItsOwn(string field, PortfolioVisualInput container) =>
        Assert.Equal(new FieldIssue($"visuals[0].{field}", FieldIssue.NotAllowed), Issue(container));

    /// <summary>Rule 2: a container is at least two rows tall, its title bar and a row for its children.</summary>
    [Fact]
    public void AContainerIsAtLeastTwoRowsTall()
    {
        Assert.Equal(new FieldIssue("visuals[0].layout", FieldIssue.OutsideRange), Issue(Container(height: 1)));
        Assert.Null(Issue(Container(height: 2)));
    }

    /// <summary>Rule 3: a child fits the inner grid: the container's columns, and its rows less the title bar's.</summary>
    [Theory]
    [InlineData(12, 6, 10, 0, 4, 2)]
    [InlineData(12, 6, 0, 4, 4, 2)]
    [InlineData(6, 6, 0, 0, 7, 2)]
    [InlineData(12, 6, -1, 0, 4, 2)]
    [InlineData(12, 6, 0, -1, 4, 2)]
    [InlineData(12, 2, 0, 0, 4, 2)]
    public void AChildFitsInsideItsContainer(short width, short height, short column, short row, short childWidth, short childHeight) =>
        Assert.Equal(
            new FieldIssue("visuals[0].children[0].layout", FieldIssue.OutsideRange),
            Issue(Container(width: width, height: height, children: [SmallCard(column, row, childWidth, childHeight)])));

    [Fact]
    public void AChildMayTakeTheWholeInnerGrid() =>
        Assert.Null(Issue(Container(width: 6, height: 6, children: [Visual(column: 0, row: 0, width: 6, height: 5)])));

    /// <summary>Rule 3: no two children overlap, as top-level visuals; a child's cells are its container's, apart from the dashboard's.</summary>
    [Fact]
    public void ChildrenOfAContainerDoNotOverlap()
    {
        Assert.Equal(new FieldIssue("visuals[0].children[1].layout", FieldIssue.InUse), Issue(Container(children: [SmallCard(0, 0), SmallCard(3, 1)])));
        Assert.Null(Issue(Container(children: [SmallCard(0, 0)]), Container(row: 6, children: [SmallCard(0, 0)])));
    }

    /// <summary>Rule 4: a child obeys every rule a top-level visual obeys.</summary>
    [Fact]
    public void AChildObeysTheRulesOfAVisual()
    {
        Assert.Equal(new FieldIssue("visuals[0].children[0].groupBy", FieldIssue.NotAllowed),
            Issue(Container(children: [Visual(PortfolioVisualType.Card, PortfolioGroupBy.Sector, width: 4, height: 2)])));
        Assert.Equal(new FieldIssue("visuals[0].children[0].measures", FieldIssue.NotAllowed),
            Issue(Container(children: [Visual(PortfolioVisualType.Pie, measures: PortfolioMeasure.AveragePlanned)])));
        Assert.Equal(new FieldIssue("visuals[0].children[0].axes", FieldIssue.NotAllowed),
            Issue(Container(children: [Visual(PortfolioVisualType.Pie) with { Axes = PortfolioAxes.Default }])));
        Assert.Equal(new FieldIssue("visuals[0].children[0].title", FieldIssue.OutsideRange),
            Issue(Container(children: [SmallCard(0, 0) with { Title = new BilingualLabel("بطاقة", new string('x', 101)) }])));
        Assert.Equal(new FieldIssue("visuals[0].children[0].widgetCode", FieldIssue.Required), Issue(Container(children: [Widget(null)])));
    }

    /// <summary>Rule 4: sections and widget codes stay unique over the whole design, children included.</summary>
    [Fact]
    public void SectionsAndWidgetCodesStayUniqueAcrossContainers()
    {
        Assert.Null(Issue(Container(height: 12, children: [Placed(PortfolioSection.PortfolioTable, 0), Widget("OPEN_RISKS", 4)])));
        Assert.Equal(new FieldIssue("visuals[1].children[0].section", FieldIssue.Duplicate),
            Issue(Placed(PortfolioSection.PortfolioTable, 0), Container(row: 4, children: [Placed(PortfolioSection.PortfolioTable, 0)])));
        Assert.Equal(new FieldIssue("visuals[1].children[1].section", FieldIssue.Duplicate),
            Issue(Placed(PortfolioSection.TopFollowUp, 0), Container(row: 4, height: 12, children: [Placed(PortfolioSection.PortfolioTable, 0), Placed(PortfolioSection.PortfolioTable, 4)])));
        Assert.Equal(new FieldIssue("visuals[1].widgetCode", FieldIssue.Duplicate),
            Issue(Container(children: [Widget("OPEN_RISKS")]), Widget("OPEN_RISKS", 6)));
        Assert.Equal(new FieldIssue("visuals[1].children[0].widgetCode", FieldIssue.Duplicate),
            Issue(Container(children: [Widget("OPEN_RISKS")]), Container(row: 6, children: [Widget("OPEN_RISKS")])));
    }

    /// <summary>A full container: 1×1 cards in its inner grid, row by row.</summary>
    private static PortfolioVisualInput Full(short row, int children) =>
        Container(row: row, width: 12, height: 24, children: [.. Enumerable.Range(0, children).Select(i => SmallCard((short)(i % 12), (short)(i / 12), 1, 1))]);

    /// <summary>Rule 5: at most 60 visuals, children counted, and at most 20 children in a container.</summary>
    [Fact]
    public void AtMostSixtyVisualsChildrenCountedAndTwentyPerContainer()
    {
        PortfolioVisualInput[] cards = [.. Enumerable.Range(0, 18).Select(i => SmallCard((short)(i % 12), (short)(48 + (i / 12)), 1, 1))];
        Assert.Null(Issue([Full(0, 20), Full(24, 20), .. cards]));
        Assert.Equal(new FieldIssue("visuals", FieldIssue.OutsideRange), Issue([Full(0, 20), Full(24, 20), .. cards, SmallCard(0, 60, 1, 1)]));
        Assert.Equal(new FieldIssue("visuals[0].children", FieldIssue.OutsideRange), Issue(Full(0, 21)));
    }

    /// <summary>Rule 6: top-level visuals, containers included, still never overlap.</summary>
    [Fact]
    public void AContainerDoesNotOverlapAnotherVisual()
    {
        Assert.Equal(new FieldIssue("visuals[1].layout", FieldIssue.InUse), Issue(Container(), Visual(row: 5)));
        Assert.Equal(new FieldIssue("visuals[1].layout", FieldIssue.InUse), Issue(Visual(), Container(row: 2, width: 6)));
        Assert.Equal(new FieldIssue("visuals[0].layout", FieldIssue.OutsideRange), Issue(Container(column: 6, width: 7)));
    }

    private static PortfolioVisual Saved(short sortOrder, PortfolioVisualType type, string title, Guid? parentId = null, short row = 0) => new()
    {
        Id = Guid.NewGuid(),
        SortOrder = sortOrder,
        ParentId = parentId,
        TitleAr = title,
        TitleEn = title,
        VisualType = type,
        Measures = type == PortfolioVisualType.Container ? [] : ["PROJECTS"],
        GroupBy = PortfolioGroupBy.None,
        LayoutColumn = 0,
        LayoutRow = row,
        LayoutWidth = 4,
        LayoutHeight = 6,
    };

    /// <summary>The saved rows read back as the design: each container holding its children in their order, the top-level visuals in theirs.</summary>
    [Fact]
    public void SavedChildrenAreNestedUnderTheirContainerInOrder()
    {
        PortfolioVisual first = Saved(1, PortfolioVisualType.Container, "First");
        PortfolioVisual second = Saved(4, PortfolioVisualType.Container, "Second", row: 6);
        IReadOnlyList<PortfolioVisualInput> design = PortfolioDesignService.Nested(
        [
            Saved(5, PortfolioVisualType.Card, "Card"),
            Saved(3, PortfolioVisualType.Card, "First B", first.Id),
            second,
            Saved(2, PortfolioVisualType.Card, "First A", first.Id),
            first,
        ]);

        Assert.Equal(["First", "Second", "Card"], design.Select(v => v.Title.En));
        Assert.Equal(["First A", "First B"], design[0].Children!.Select(v => v.Title.En));
        Assert.Empty(design[1].Children!);
        Assert.Null(design[2].Children);
    }

    /// <summary>A child is calculated as the same visual on the dashboard itself; a container carries no figures and a non-container no children.</summary>
    [Fact]
    public void AChildIsCalculatedAsATopLevelVisual()
    {
        PortfolioProjectRow a = Row(), b = Row(ProjectLifecycleState.Draft), c = Row();
        IReadOnlyList<PortfolioProjectRow> rows = [a, b, c];
        Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells = [];
        IReadOnlyDictionary<string, PortfolioFieldType> types = new Dictionary<string, PortfolioFieldType>(StringComparer.Ordinal);

        // The status breakdown is held, the sector one is not: its visual has no groups, inside a container as outside.
        IReadOnlyList<PortfolioVisualGroup>? GroupsOf(PortfolioVisualInput v) =>
            v.GroupBy == PortfolioGroupBy.Sector ? null : PortfolioDesignService.Groups(v, rows, null, amounts: true, Thresholds, cells, ByKey);

        PortfolioVisualInput card = SmallCard(0, 0) with { Measures = [PortfolioMeasure.Projects, PortfolioMeasure.InProgress] };
        PortfolioVisualInput bar = Visual(groupBy: PortfolioGroupBy.Status, column: 4, row: 0, width: 8, height: 5);
        PortfolioVisualInput withheld = Visual(column: 0, row: 2, width: 4, height: 3);

        PortfolioVisualResult container = PortfolioDesignService.ResultOf(Container(children: [card, bar, withheld]), GroupsOf, types);

        Assert.Equal(PortfolioVisualType.Container, container.VisualType);
        Assert.Empty(container.Groups!);
        Assert.Equal([PortfolioVisualType.Card, PortfolioVisualType.Bar, PortfolioVisualType.Bar], container.Children!.Select(v => v.VisualType));
        Assert.Equivalent(PortfolioDesignService.ResultOf(card, GroupsOf, types), container.Children![0], strict: true);
        Assert.Equivalent(PortfolioDesignService.ResultOf(bar, GroupsOf, types), container.Children![1], strict: true);
        Assert.Equal([3m, 2m], container.Children![0].Groups!.Single().Values);
        Assert.Equal(["DRAFT", "ACTIVE"], container.Children![1].Groups!.Select(g => g.Key));
        Assert.Null(container.Children![2].Groups);
        Assert.All(container.Children!, child => Assert.Null(child.Children));
    }

    private static PortfolioTableColumn Dim(string key, PortfolioTableAggregation aggregation = PortfolioTableAggregation.None) => new(PortfolioTableColumnKind.Dimension, key, aggregation);

    private static PortfolioTableColumn Fld(string key, PortfolioTableAggregation aggregation) => new(PortfolioTableColumnKind.Field, key, aggregation);

    private static PortfolioTableColumn Msr(string key) => new(PortfolioTableColumnKind.Measure, key, null);

    /// <summary>A TABLE visual of columns, drawing no measure of its own.</summary>
    private static PortfolioVisualInput ColumnTable(params PortfolioTableColumn[] columns) =>
        new(new BilingualLabel("جدول", "Table"), PortfolioVisualType.Table, [], PortfolioGroupBy.None, 0, 0, 12, 6, Columns: columns);

    private static readonly Guid Health1 = Guid.NewGuid(), Education = Guid.NewGuid();

    private static readonly Dictionary<string, BilingualLabel> SectorNames = new(StringComparer.Ordinal)
    {
        [Health1.ToString()] = new BilingualLabel("الصحة", "Health"),
        [Education.ToString()] = new BilingualLabel("التعليم", "Education"),
    };

    /// <summary>A project of a sector with a budget, progress (planned, actual) and its on-plan label as the indicators calculate it.</summary>
    private static PortfolioProjectRow Project(
        Guid sector, decimal? budget = null, (decimal Planned, decimal Actual)? progress = null, ProjectLifecycleState state = ProjectLifecycleState.Active,
        string title = "مشروع", string? formalId = null, Guid? department = null)
    {
        decimal? gap = progress is { } pa ? pa.Actual - pa.Planned : null;
        ProjectIndicators indicators = progress is { } p
            ? new ProjectIndicators(gap, PortfolioInsightsService.LabelOf(gap, Thresholds), null, null, [],
                Progress: ProjectOverallProgress.None with { PlannedPercent = p.Planned, ActualPercent = p.Actual })
            : ProjectIndicators.None;
        return new(new PMPlatform.Domain.Project.Project
        {
            Id = Guid.NewGuid(),
            Title = new NarrativeText(title, Language.Ar),
            LifecycleState = state,
            ClassificationItemId = sector,
            FormalProjectId = formalId,
            RegistrationBudgetSar = budget is { } b ? new Money(b) : null,
            DepartmentId = department ?? Guid.Empty,
        }, indicators);
    }

    private static PortfolioTableResult Table(
        PortfolioVisualInput visual, IReadOnlyList<PortfolioProjectRow> rows, bool amounts = true,
        Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>>? cells = null) =>
        PortfolioDesignService.Table(visual, rows, null, amounts, Thresholds, cells ?? [], ByKey, SectorNames);

    [Fact]
    public void ATableDrawsColumns() =>
        Assert.Null(Issue(ColumnTable(Dim("SECTOR"), Msr("PROJECTS"), Fld(Budget, PortfolioTableAggregation.Sum), Dim("TITLE", PortfolioTableAggregation.CountDistinct))));

    [Theory]
    [InlineData(PortfolioVisualType.Bar)]
    [InlineData(PortfolioVisualType.Card)]
    [InlineData(PortfolioVisualType.Column)]
    public void OnlyATableDrawsColumns(PortfolioVisualType type) =>
        Assert.Equal(new FieldIssue("visuals[0].columns", FieldIssue.NotAllowed), Issue(ColumnTable(Dim("SECTOR")) with { VisualType = type }));

    [Fact]
    public void ASectionOrContainerDrawsNoColumns()
    {
        Assert.Equal(new FieldIssue("visuals[0].columns", FieldIssue.NotAllowed), Issue(Placed(PortfolioSection.SectorTable, 0) with { Columns = [Dim("SECTOR")] }));
        Assert.Equal(new FieldIssue("visuals[0].columns", FieldIssue.NotAllowed), Issue(Container() with { Columns = [Dim("SECTOR")] }));
    }

    private static readonly string[] TwelveDimensions = ["PORTFOLIO", "INITIATIVE", "SECTOR", "DEPARTMENT", "ENTITY", "STATUS", "STAGE", "ON_PLAN", "REGION", "CITY", "PRIORITY", "TITLE"];

    [Fact]
    public void ATableDrawsOneToTwelveColumns()
    {
        PortfolioTableColumn[] twelve = [.. TwelveDimensions.Select(k => Dim(k))];
        Assert.Null(Issue(ColumnTable(twelve)));
        Assert.Equal(new FieldIssue("visuals[0].columns", FieldIssue.OutsideRange), Issue(ColumnTable([.. twelve, Dim("FORMAL_PROJECT_ID")])));
    }

    [Theory]
    [InlineData(PortfolioTableColumnKind.Dimension, "SECTORS", PortfolioTableAggregation.None, "visuals[0].columns[0].key")]
    [InlineData(PortfolioTableColumnKind.Dimension, "PROJECT.SECTOR", PortfolioTableAggregation.None, "visuals[0].columns[0].key")]
    [InlineData(PortfolioTableColumnKind.Measure, "SECTOR", null, "visuals[0].columns[0].key")]
    [InlineData(PortfolioTableColumnKind.Field, "PROGRESS.PUBLISHED_PROGRESS_HISTORY.ACTUAL_PERCENT", PortfolioTableAggregation.Average, "visuals[0].columns[0].key")]
    [InlineData(PortfolioTableColumnKind.Field, "PROJECTS", PortfolioTableAggregation.Count, "visuals[0].columns[0].key")]
    [InlineData((PortfolioTableColumnKind)9, "SECTOR", PortfolioTableAggregation.None, "visuals[0].columns[0].kind")]
    public void AColumnIsAKnownKeyOfItsKind(PortfolioTableColumnKind kind, string key, PortfolioTableAggregation? aggregation, string field) =>
        Assert.Equal(new FieldIssue(field, FieldIssue.NotAllowed), Issue(ColumnTable(new PortfolioTableColumn(kind, key, aggregation))));

    [Theory]
    [InlineData(PortfolioTableColumnKind.Dimension, "SECTOR", PortfolioTableAggregation.Sum, FieldIssue.NotAllowed)]
    [InlineData(PortfolioTableColumnKind.Dimension, "SECTOR", null, FieldIssue.Required)]
    [InlineData(PortfolioTableColumnKind.Measure, "PROJECTS", PortfolioTableAggregation.None, FieldIssue.NotAllowed)]
    [InlineData(PortfolioTableColumnKind.Measure, "PROJECTS", PortfolioTableAggregation.Count, FieldIssue.NotAllowed)]
    [InlineData(PortfolioTableColumnKind.Field, Health, PortfolioTableAggregation.Sum, FieldIssue.NotAllowed)]
    [InlineData(PortfolioTableColumnKind.Field, ActualPercent, PortfolioTableAggregation.Sum, FieldIssue.NotAllowed)]
    [InlineData(PortfolioTableColumnKind.Field, Strategic, PortfolioTableAggregation.Average, FieldIssue.NotAllowed)]
    [InlineData(PortfolioTableColumnKind.Field, Budget, null, FieldIssue.Required)]
    public void AColumnTakesTheCalculationsItAllows(PortfolioTableColumnKind kind, string key, PortfolioTableAggregation? aggregation, string code) =>
        Assert.Equal(new FieldIssue("visuals[0].columns[0].aggregation", code), Issue(ColumnTable(new PortfolioTableColumn(kind, key, aggregation))));

    [Theory]
    [InlineData(PortfolioTableColumnKind.Dimension, "STATUS", PortfolioTableAggregation.Count)]
    [InlineData(PortfolioTableColumnKind.Dimension, "ENTITY", PortfolioTableAggregation.CountDistinct)]
    [InlineData(PortfolioTableColumnKind.Field, Health, PortfolioTableAggregation.None)]
    [InlineData(PortfolioTableColumnKind.Field, Health, PortfolioTableAggregation.CountDistinct)]
    [InlineData(PortfolioTableColumnKind.Field, Strategic, PortfolioTableAggregation.ShareTrue)]
    [InlineData(PortfolioTableColumnKind.Field, PlannedEnd, PortfolioTableAggregation.Max)]
    [InlineData(PortfolioTableColumnKind.Field, Budget, PortfolioTableAggregation.Average)]
    [InlineData(PortfolioTableColumnKind.Measure, "PROJECT_SHARE", null)]
    [InlineData(PortfolioTableColumnKind.Measure, "ON_PLAN", null)]
    [InlineData(PortfolioTableColumnKind.Measure, "MEASURED", null)]
    public void AColumnTakesItsKindsCalculations(PortfolioTableColumnKind kind, string key, PortfolioTableAggregation? aggregation) =>
        Assert.Null(Issue(ColumnTable(new PortfolioTableColumn(kind, key, aggregation))));

    [Fact]
    public void AColumnIsNamedOnce()
    {
        Assert.Equal(new FieldIssue("visuals[0].columns[1]", FieldIssue.Duplicate), Issue(ColumnTable(Dim("SECTOR"), Dim("SECTOR"))));
        Assert.Null(Issue(ColumnTable(Dim("SECTOR"), Dim("SECTOR", PortfolioTableAggregation.Count))));
        Assert.Equal(new FieldIssue("visuals[0].columns[1]", FieldIssue.Duplicate), Issue(ColumnTable(Msr("PROJECTS"), Msr("PROJECTS"))));
    }

    [Fact]
    public void AColumnTableDrawsNothingElse()
    {
        PortfolioVisualInput table = ColumnTable(Dim("SECTOR"), Msr("PROJECTS"));
        Assert.Equal(new FieldIssue("visuals[0].measures", FieldIssue.NotAllowed), Issue(table with { Measures = [PortfolioMeasure.Projects] }));
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.NotAllowed), Issue(table with { Values = [Value(Budget, PortfolioFieldAggregation.Sum)] }));
        Assert.Equal(new FieldIssue("visuals[0].groupBy", FieldIssue.NotAllowed), Issue(table with { GroupBy = PortfolioGroupBy.Sector }));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(table with { GroupField = Health }));
        Assert.Null(Issue(table with { Filters = [new(Health, ["RED"]), new("PROJECT.STATUS", ["ACTIVE"])] }));
        Assert.Equal(new FieldIssue("visuals[0].filters", FieldIssue.NotAllowed), Issue(table with { Filters = [new(Budget, ["1"])] }));
    }

    [Theory]
    [InlineData("SECTOR", "PORTFOLIO_BREAKDOWN_VIEW")]
    [InlineData("STATUS", "PORTFOLIO_STATUS_VIEW")]
    [InlineData("TITLE", "PORTFOLIO_SUMMARY_VIEW")]
    public void AColumnIsReadUnderItsGroupingsPermission(string key, string permission)
    {
        Assert.Equal([permission], PortfolioDesignService.PermissionsOf(ColumnTable(Dim(key), Msr("PROJECTS"))));
        Assert.Equal(
            ["PORTFOLIO_BREAKDOWN_VIEW", "PORTFOLIO_STATUS_VIEW"],
            PortfolioDesignService.PermissionsOf(ColumnTable(Dim("SECTOR"), Dim("STATUS", PortfolioTableAggregation.Count), Dim("CITY"))));
        Assert.Equal(["PORTFOLIO_SUMMARY_VIEW"], PortfolioDesignService.PermissionsOf(ColumnTable(Msr("PROJECTS"), Fld(Budget, PortfolioTableAggregation.Sum))));
    }

    /// <summary>The rows are grouped by the NONE column, ordered by its Arabic name; each measure is the insights' breakdown row for that sector.</summary>
    [Fact]
    public void AMeasureOfARowIsTheInsightsBreakdownRowOfItsGroup()
    {
        List<PortfolioProjectRow> rows =
        [
            Project(Health1, 100m, (50m, 40m)), Project(Health1, 50.5m, (20m, 25m)), Project(Health1, null, null, ProjectLifecycleState.Suspended),
            Project(Education, 10m, (60m, 45m)), Project(Education, null, (30m, 30m)),
        ];
        PortfolioTableResult table = Table(
            ColumnTable(Dim("SECTOR"), Msr("PROJECTS"), Msr("VALUE_SAR"), Msr("MEASURED"), Msr("AVERAGE_PLANNED"), Msr("AVERAGE_ACTUAL"), Msr("GAP_POINTS"), Msr("ON_PLAN"),
                Msr("IN_PROGRESS")),
            rows);

        Assert.Equal(
            [PortfolioTableColumnType.Item, PortfolioTableColumnType.Count, PortfolioTableColumnType.Sar, PortfolioTableColumnType.Count, PortfolioTableColumnType.Percent,
                PortfolioTableColumnType.Percent, PortfolioTableColumnType.Points, PortfolioTableColumnType.OnPlan, PortfolioTableColumnType.Count],
            table.ColumnTypes);

        // التعليم before الصحة: by the Arabic name.
        Assert.Equal([Education.ToString(), Health1.ToString()], table.Rows.Select(r => r.Cells[0].Key));
        Assert.Equal(["التعليم", "الصحة"], table.Rows.Select(r => r.Cells[0].Label!.Ar));
        foreach (PortfolioTableRow row in table.Rows)
        {
            List<PortfolioProjectRow> group = [.. rows.Where(r => r.Project.ClassificationItemId.ToString() == row.Cells[0].Key)];
            PortfolioBreakdownRow expected = PortfolioInsightsService.RowOf(Guid.Parse(row.Cells[0].Key!), null, group, amounts: true, Thresholds, sector: true);
            Assert.Equal(expected.Projects, row.Projects);
            Assert.Equal(
                [expected.Projects, decimal.Parse(expected.ValueSar!, System.Globalization.CultureInfo.InvariantCulture), expected.Measured, expected.AveragePlannedPercent,
                    expected.AverageActualPercent, expected.GapPoints],
                row.Cells.Skip(1).Take(6).Select(c => c.Value));
            Assert.Equal(expected.OnPlan is { } onPlan ? JsonNamingPolicyCode(onPlan) : null, row.Cells[7].Key);
            Assert.Null(row.Cells[7].Value);
            Assert.Null(row.ProjectId);
        }

        Assert.Equal([2m, 2m], table.Rows.Select(r => r.Cells[8].Value));
        Assert.Equal("LATE", table.Rows[0].Cells[7].Key);
        Assert.False(table.Truncated);
    }

    private static string JsonNamingPolicyCode(OnPlanStatus status) => System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(status.ToString());

    /// <summary>The totals are calculated over every row's projects, never from the rows; a grouped column has no total; off with showTotals false.</summary>
    [Fact]
    public void TheTotalsAreCalculatedOverAllTheProjects()
    {
        List<PortfolioProjectRow> rows = [Project(Health1, 100m, (50m, 40m)), Project(Health1, 20m, (20m, 25m)), Project(Education, 10m, (60m, 45m))];
        PortfolioTableResult table = Table(ColumnTable(Dim("SECTOR"), Msr("PROJECTS"), Msr("VALUE_SAR"), Msr("AVERAGE_PLANNED"), Msr("PROJECT_SHARE")), rows);
        PortfolioBreakdownRow all = PortfolioInsightsService.RowOf(null, null, rows, amounts: true, Thresholds, sector: false);

        Assert.Equal(new PortfolioTableCell(null, null, null), table.Totals![0]);
        Assert.Equal([3m, 130m, all.AveragePlannedPercent, 100m], table.Totals.Skip(1).Select(c => c.Value));
        Assert.Equal([33.3m, 66.7m], table.Rows.Select(r => r.Cells[4].Value));
        Assert.Null(Table(ColumnTable(Dim("SECTOR"), Msr("PROJECTS")) with { ShowTotals = false }, rows).Totals);
    }

    /// <summary>Two NONE columns group by the pair, ordered by the first then the second (a status in lifecycle order); no NONE column draws one row.</summary>
    [Fact]
    public void TwoGroupedColumnsGroupByThePair()
    {
        List<PortfolioProjectRow> rows =
        [
            Project(Health1), Project(Health1, state: ProjectLifecycleState.Suspended), Project(Health1), Project(Education, state: ProjectLifecycleState.Suspended),
        ];
        PortfolioTableResult table = Table(ColumnTable(Dim("SECTOR"), Dim("STATUS"), Msr("PROJECTS")), rows);

        Assert.Equal(
            [(Education.ToString(), "SUSPENDED", 1m), (Health1.ToString(), "ACTIVE", 2m), (Health1.ToString(), "SUSPENDED", 1m)],
            table.Rows.Select(r => (r.Cells[0].Key, r.Cells[1].Key, r.Cells[2].Value!.Value)));
        Assert.Equal([PortfolioTableColumnType.Item, PortfolioTableColumnType.Code, PortfolioTableColumnType.Count], table.ColumnTypes);

        PortfolioTableResult ungrouped = Table(ColumnTable(Msr("PROJECTS"), Dim("SECTOR", PortfolioTableAggregation.CountDistinct)), rows);
        Assert.Equal(4m, ungrouped.Rows.Single().Cells[0].Value);
        Assert.Equal(2m, ungrouped.Rows.Single().Cells[1].Value);
        Assert.Single(Table(ColumnTable(Msr("PROJECTS")), []).Rows);
        Assert.Empty(Table(ColumnTable(Dim("SECTOR"), Msr("PROJECTS")), []).Rows);
    }

    /// <summary>The title draws one row per project, named by its title and carrying its id; the formal ID too, counted where it is set.</summary>
    [Fact]
    public void ATitleColumnDrawsARowPerProject()
    {
        PortfolioProjectRow a = Project(Health1, title: "ب مشروع", formalId: "P-2"), b = Project(Health1, title: "أ مشروع", formalId: null), c = Project(Education, title: "أ مشروع", formalId: "P-1");
        PortfolioTableResult table = Table(ColumnTable(Dim("TITLE"), Dim("FORMAL_PROJECT_ID"), Msr("PROJECTS")), [a, b, c]);

        Assert.Equal(3, table.Rows.Count);
        // c and b share a title, so the formal ID orders them, one missing last.
        Assert.Equal([c.Project.Id, b.Project.Id, a.Project.Id], table.Rows.Select(r => r.ProjectId!.Value));
        Assert.Equal(a.Project.Id, table.Rows[2].ProjectId);
        Assert.All(table.Rows, r => Assert.Equal(r.ProjectId.ToString(), r.Cells[0].Key));
        Assert.Equal("ب مشروع", table.Rows[2].Cells[0].Label!.Ar);
        Assert.Equal([PortfolioTableColumnType.Text, PortfolioTableColumnType.Code, PortfolioTableColumnType.Count], table.ColumnTypes);

        PortfolioTableResult counted = Table(ColumnTable(Dim("SECTOR"), Dim("FORMAL_PROJECT_ID", PortfolioTableAggregation.Count), Dim("TITLE", PortfolioTableAggregation.CountDistinct)), [a, b, c]);
        Assert.Equal([(1m, 1m), (1m, 2m)], counted.Rows.Select(r => (r.Cells[1].Value!.Value, r.Cells[2].Value!.Value)));
    }

    /// <summary>A field column: grouped by its value, summed, averaged and counted distinct over the revealed cells; a sum with none revealed is null.</summary>
    [Fact]
    public void AFieldColumnIsCalculatedOverItsRevealedCells()
    {
        PortfolioProjectRow a = Project(Health1), b = Project(Health1), c = Project(Education), d = Project(Education);
        Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells = new()
        {
            [a.Project.Id] = Cells((Health, "RED"), (Budget, "100.00"), (Strategic, "true")),
            [b.Project.Id] = Cells((Health, "GREEN"), (Budget, "50.00"), (Strategic, "false")),
            [c.Project.Id] = Cells((Health, "RED"), (Budget, null), (Strategic, "true")),
            [d.Project.Id] = Cells((Health, "RED"), (Budget, null), (Strategic, null)),
        };

        PortfolioTableResult table = Table(
            ColumnTable(Dim("SECTOR"), Fld(Budget, PortfolioTableAggregation.Sum), Fld(Budget, PortfolioTableAggregation.Average), Fld(Health, PortfolioTableAggregation.CountDistinct),
                Fld(Strategic, PortfolioTableAggregation.ShareTrue), Fld(Budget, PortfolioTableAggregation.Count)),
            [a, b, c, d], cells: cells);

        Assert.Equal([null, null, 1m, 100m, 0m], table.Rows[0].Cells.Skip(1).Select(x => x.Value));
        Assert.Equal([150m, 75m, 2m, 50m, 2m], table.Rows[1].Cells.Skip(1).Select(x => x.Value));
        Assert.Equal(
            [PortfolioTableColumnType.Item, PortfolioTableColumnType.Sar, PortfolioTableColumnType.Sar, PortfolioTableColumnType.Count, PortfolioTableColumnType.Percent,
                PortfolioTableColumnType.Count],
            table.ColumnTypes);

        PortfolioTableResult byHealth = Table(ColumnTable(Fld(Health, PortfolioTableAggregation.None), Msr("PROJECTS")), [a, b, c, d], cells: cells);
        Assert.Equal([("GREEN", 1m), ("RED", 3m)], byHealth.Rows.Select(r => (r.Cells[0].Key!, r.Cells[1].Value!.Value)));
    }

    /// <summary>Without FINANCIAL_VIEW_SENSITIVE the value and a sensitive field are masked, in the rows and the totals; counts are not.</summary>
    [Fact]
    public void AmountsAreMaskedWithoutTheAmountsPermission()
    {
        PortfolioProjectRow a = Project(Health1, 100m);
        Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>> cells = new() { [a.Project.Id] = Cells((Budget, null)) };
        PortfolioTableResult table = Table(ColumnTable(Dim("SECTOR"), Msr("VALUE_SAR"), Fld(Budget, PortfolioTableAggregation.Sum), Msr("PROJECTS")), [a], amounts: false, cells);

        foreach (IReadOnlyList<PortfolioTableCell> row in new[] { table.Rows[0].Cells, table.Totals! })
        {
            Assert.Equal(new PortfolioTableCell(null, null, null, Masked: true), row[1]);
            Assert.Equal(new PortfolioTableCell(null, null, null, Masked: true), row[2]);
            Assert.Equal(new PortfolioTableCell(null, null, 1m), row[3]);
        }

        Assert.False(Table(ColumnTable(Dim("SECTOR"), Msr("VALUE_SAR")), [a]).Rows[0].Cells[1].Masked);
    }

    /// <summary>At most 500 rows, the totals still over every project.</summary>
    [Fact]
    public void ATableDrawsAtMostFiveHundredRows()
    {
        List<PortfolioProjectRow> rows = [.. Enumerable.Range(0, 501).Select(i => Project(Health1, title: $"مشروع {i:D3}"))];
        PortfolioTableResult table = Table(ColumnTable(Dim("TITLE"), Msr("PROJECTS")), rows);

        Assert.Equal(500, table.Rows.Count);
        Assert.True(table.Truncated);
        Assert.Equal(501m, table.Totals![1].Value);
        Assert.Equal("مشروع 000", table.Rows[0].Cells[0].Label!.Ar);
        Assert.False(Table(ColumnTable(Dim("TITLE"), Msr("PROJECTS")), [.. rows.Take(500)]).Truncated);
    }

    /// <summary>The visual's filters narrow the table's projects.</summary>
    [Fact]
    public void FiltersNarrowATable()
    {
        List<PortfolioProjectRow> rows = [Project(Health1), Project(Health1, state: ProjectLifecycleState.Suspended), Project(Education)];
        PortfolioTableResult table = Table(ColumnTable(Dim("SECTOR"), Msr("PROJECTS")) with { Filters = [new("PROJECT.STATUS", ["ACTIVE"])] }, rows);
        Assert.Equal([1m, 1m], table.Rows.Select(r => r.Cells[1].Value));
        Assert.Equal(2m, table.Totals![1].Value);
    }

    /// <summary>A column table echoes its columns, has no groups and its figures in table; without its permission, neither.</summary>
    [Fact]
    public void AColumnTableIsEchoedWithItsTable()
    {
        PortfolioVisualInput visual = ColumnTable(Dim("SECTOR"), Msr("PROJECTS")) with { ShowTotals = false };
        PortfolioTableResult figures = Table(visual, [Project(Health1)]);
        IReadOnlyDictionary<string, PortfolioFieldType> types = new Dictionary<string, PortfolioFieldType>(StringComparer.Ordinal);

        PortfolioVisualResult result = PortfolioDesignService.ResultOf(visual, _ => throw new InvalidOperationException(), types, _ => figures);
        Assert.Empty(result.Groups!);
        Assert.Same(figures, result.Table);
        Assert.Equal(visual.Columns, result.Columns);
        Assert.False(result.ShowTotals);
        Assert.Empty(result.Measures);

        PortfolioVisualResult withheld = PortfolioDesignService.ResultOf(visual, _ => [], types, _ => null);
        Assert.Null(withheld.Groups);
        Assert.Null(withheld.Table);
    }

    /// <summary>The box sections' nine figures (dashboard-designer-box-sections.md).</summary>
    private static readonly PortfolioMeasure[] BoxFigures =
    [
        PortfolioMeasure.OnTrack, PortfolioMeasure.Late, PortfolioMeasure.Stalled, PortfolioMeasure.NotMeasured, PortfolioMeasure.FollowUpStalled,
        PortfolioMeasure.FollowUpSuspended, PortfolioMeasure.FollowUpLate, PortfolioMeasure.FollowUpActionOverdue, PortfolioMeasure.FollowUpOpenObstacle,
    ];

    /// <summary>A project of a sector with its on-plan status and its follow-up reasons, most urgent first, as the indicators calculate them.</summary>
    private static PortfolioProjectRow Boxed(Guid sector, OnPlanStatus? onPlan, params FollowUpReason[] reasons) =>
        new(new PMPlatform.Domain.Project.Project
        {
            Id = Guid.NewGuid(),
            Title = new NarrativeText("مشروع", Language.Ar),
            LifecycleState = ProjectLifecycleState.Active,
            ClassificationItemId = sector,
        }, ProjectIndicators.None with { OnPlan = onPlan, FollowUpReasons = reasons });

    /// <summary>Every on-plan status and follow-up reason, some projects with several reasons (counted under their first) and some with none.</summary>
    private static List<PortfolioProjectRow> BoxedPopulation() =>
    [
        Boxed(Health1, OnPlanStatus.OnTrack), Boxed(Health1, OnPlanStatus.OnTrack, FollowUpReason.OpenObstacle),
        Boxed(Health1, OnPlanStatus.Late, FollowUpReason.Late, FollowUpReason.ActionOverdue), Boxed(Health1, OnPlanStatus.Stalled, FollowUpReason.Stalled, FollowUpReason.Late),
        Boxed(Health1, null, FollowUpReason.Suspended), Boxed(Education, OnPlanStatus.Late, FollowUpReason.Late), Boxed(Education, OnPlanStatus.OnTrack, FollowUpReason.ActionOverdue, FollowUpReason.OpenObstacle),
        Boxed(Education, null), Boxed(Education, OnPlanStatus.Stalled, FollowUpReason.Stalled), Boxed(Education, null, FollowUpReason.OpenObstacle),
    ];

    /// <summary>A box section's figure over a population, as the insights' section shows it.</summary>
    private static int BoxOf(PortfolioMeasure measure, IReadOnlyList<PortfolioProjectRow> rows)
    {
        PortfolioOnPlan onPlan = PortfolioInsightsService.OnPlanOf(rows);
        PortfolioFollowUpReasons reasons = PortfolioInsightsService.Reasons(rows);
        return measure switch
        {
            PortfolioMeasure.OnTrack => onPlan.OnTrack,
            PortfolioMeasure.Late => onPlan.Late,
            PortfolioMeasure.Stalled => onPlan.Stalled,
            PortfolioMeasure.NotMeasured => onPlan.NotMeasured,
            PortfolioMeasure.FollowUpStalled => reasons.Stalled,
            PortfolioMeasure.FollowUpSuspended => reasons.Suspended,
            PortfolioMeasure.FollowUpLate => reasons.Late,
            PortfolioMeasure.FollowUpActionOverdue => reasons.OverdueAction,
            PortfolioMeasure.FollowUpOpenObstacle => reasons.OpenObstacle,
            PortfolioMeasure.NeedingFollowUp => reasons.Total,
            PortfolioMeasure.Projects or PortfolioMeasure.InProgress or PortfolioMeasure.ValueSar or PortfolioMeasure.AveragePlanned or PortfolioMeasure.AverageActual
                or PortfolioMeasure.GapPoints or PortfolioMeasure.OverdueActions => throw new ArgumentOutOfRangeException(nameof(measure), measure, "Not a box."),
            _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, "Not a box."),
        };
    }

    /// <summary>
    /// Each box figure on a card is the insights' box for the same projects, and the figures add up as the sections' boxes do: the four on-plan boxes to
    /// every project, the five reasons to the reasons' total, which is NEEDING_FOLLOW_UP.
    /// </summary>
    [Fact]
    public void ABoxFigureOnACardIsTheSectionsBox()
    {
        List<PortfolioProjectRow> rows = BoxedPopulation();
        decimal? CardOf(PortfolioMeasure measure) =>
            Assert.Single(Groups(Visual(PortfolioVisualType.Card, PortfolioGroupBy.None, measures: [measure]), rows, [])).Values[0];

        foreach (PortfolioMeasure measure in BoxFigures)
        {
            Assert.Equal(BoxOf(measure, rows), CardOf(measure));
        }

        Assert.Equal([3m, 2m, 2m, 3m], BoxFigures[..4].Select(CardOf).Select(v => v!.Value));
        Assert.Equal([2m, 1m, 2m, 1m, 2m], BoxFigures[4..].Select(CardOf).Select(v => v!.Value));
        Assert.Equal(rows.Count, BoxFigures[..4].Sum(m => CardOf(m)!.Value));
        Assert.Equal(CardOf(PortfolioMeasure.NeedingFollowUp), BoxFigures[4..].Sum(CardOf));
        Assert.Equal(8m, CardOf(PortfolioMeasure.NeedingFollowUp));
        Assert.Equal(PortfolioInsightsService.Reasons(rows).Total, CardOf(PortfolioMeasure.NeedingFollowUp));
    }

    /// <summary>A grouped chart counts each group's box as the section would over that group's projects.</summary>
    [Fact]
    public void ABoxFigureOfAGroupIsTheSectionsBoxOfItsProjects()
    {
        List<PortfolioProjectRow> rows = BoxedPopulation();
        foreach (PortfolioMeasure measure in BoxFigures)
        {
            foreach (PortfolioVisualGroup group in Groups(Visual(PortfolioVisualType.Bar, PortfolioGroupBy.Sector, measures: [measure]), rows, []))
            {
                Assert.Equal(BoxOf(measure, [.. rows.Where(r => r.Project.ClassificationItemId.ToString() == group.Key)]), group.Values[0]);
            }
        }
    }

    /// <summary>Each box figure is a MEASURE column of the Power table: a count per row over the row's projects, and over all of them in the totals.</summary>
    [Fact]
    public void ABoxFigureIsAPowerTableMeasureColumn()
    {
        List<PortfolioProjectRow> rows = BoxedPopulation();
        PortfolioMeasure[] measured = [.. BoxFigures, PortfolioMeasure.NeedingFollowUp];
        List<string> codes = [.. measured.Select(m => System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(m.ToString()))];
        Assert.Equal(
            ["ON_TRACK", "LATE", "STALLED", "NOT_MEASURED", "FOLLOW_UP_STALLED", "FOLLOW_UP_SUSPENDED", "FOLLOW_UP_LATE", "FOLLOW_UP_ACTION_OVERDUE", "FOLLOW_UP_OPEN_OBSTACLE",
                "NEEDING_FOLLOW_UP"],
            codes);
        PortfolioVisualInput visual = ColumnTable([Dim("SECTOR"), .. codes.Select(Msr)]);
        Assert.Null(Issue(visual));

        PortfolioTableResult table = Table(visual, rows);
        Assert.All(table.ColumnTypes.Skip(1), type => Assert.Equal(PortfolioTableColumnType.Count, type));
        Assert.Equal(2, table.Rows.Count);
        foreach (PortfolioTableRow row in table.Rows)
        {
            List<PortfolioProjectRow> group = [.. rows.Where(r => r.Project.ClassificationItemId.ToString() == row.Cells[0].Key)];
            Assert.Equal(measured.Select(m => (decimal?)BoxOf(m, group)), row.Cells.Skip(1).Select(c => c.Value));
        }

        Assert.Equal(measured.Select(m => (decimal?)BoxOf(m, rows)), table.Totals!.Skip(1).Select(c => c.Value));
    }

    /// <summary>A box figure is accepted wherever a count is (a card, bars, columns, a pie, a donut, a table) and refused only where a count is (a gauge).</summary>
    [Fact]
    public void ABoxFigureIsAcceptedWhereCountsAre()
    {
        foreach (PortfolioMeasure measure in BoxFigures)
        {
            foreach (PortfolioMeasure count in (PortfolioMeasure[])[measure, PortfolioMeasure.Projects])
            {
                Assert.Null(Issue(Visual(PortfolioVisualType.Card, PortfolioGroupBy.None, measures: [count])));
                Assert.Null(Issue(Visual(PortfolioVisualType.Bar, measures: [count])));
                Assert.Null(Issue(Visual(PortfolioVisualType.Pie, measures: [count])));
                Assert.Null(Issue(Visual(PortfolioVisualType.Donut, PortfolioGroupBy.OnPlan, measures: [count])));
                Assert.Null(Issue(Visual(PortfolioVisualType.Table, PortfolioGroupBy.None, measures: [count])));
                Assert.Equal(new FieldIssue("visuals[0].measures", FieldIssue.NotAllowed), Issue(Visual(PortfolioVisualType.Gauge, PortfolioGroupBy.None, measures: [count])));
            }

            Assert.Null(Issue(Visual(PortfolioVisualType.Column, measures: [PortfolioMeasure.Projects, measure, PortfolioMeasure.NeedingFollowUp])));
            Assert.Null(Issue(Visual(PortfolioVisualType.Card, PortfolioGroupBy.None, measures: [PortfolioMeasure.Projects, measure])));
        }
    }

    /// <summary>
    /// A box figure needs its section's permission besides its grouping's; an ungrouped visual of box figures only is read under its sections' permissions alone,
    /// as the sections are; a table column the same.
    /// </summary>
    [Fact]
    public void ABoxFigureIsReadUnderItsSectionsPermission()
    {
        foreach (PortfolioMeasure measure in BoxFigures)
        {
            Assert.NotNull(PortfolioDesignService.PermissionOf(measure));
        }

        Assert.Null(PortfolioDesignService.PermissionOf(PortfolioMeasure.NeedingFollowUp));
        string status = PermissionCatalogue.PortfolioStatusView, followUp = PermissionCatalogue.PortfolioFollowUpView;
        Assert.Equal([status], PortfolioDesignService.PermissionsOf(Visual(PortfolioVisualType.Card, PortfolioGroupBy.None, measures: [PortfolioMeasure.OnTrack])));
        Assert.Equal([followUp], PortfolioDesignService.PermissionsOf(Visual(PortfolioVisualType.Card, PortfolioGroupBy.None, measures: [PortfolioMeasure.FollowUpLate])));
        Assert.Equal(
            [status, followUp],
            PortfolioDesignService.PermissionsOf(Visual(PortfolioVisualType.Card, PortfolioGroupBy.None, measures: [PortfolioMeasure.Late, PortfolioMeasure.FollowUpLate])));
        Assert.Equal(
            [PermissionCatalogue.PortfolioSummaryView, status],
            PortfolioDesignService.PermissionsOf(Visual(PortfolioVisualType.Card, PortfolioGroupBy.None, measures: [PortfolioMeasure.Projects, PortfolioMeasure.OnTrack])));
        Assert.Equal(
            [PermissionCatalogue.PortfolioBreakdownView, followUp], PortfolioDesignService.PermissionsOf(Visual(measures: [PortfolioMeasure.FollowUpStalled])));
        Assert.Equal([status], PortfolioDesignService.PermissionsOf(Visual(PortfolioVisualType.Donut, PortfolioGroupBy.OnPlan, measures: [PortfolioMeasure.Stalled])));
        Assert.Equal([followUp], PortfolioDesignService.PermissionsOf(ColumnTable(Msr("FOLLOW_UP_LATE"))));
        Assert.Equal([PermissionCatalogue.PortfolioBreakdownView, status], PortfolioDesignService.PermissionsOf(ColumnTable(Dim("SECTOR"), Msr("ON_TRACK"))));
        Assert.Equal(
            [PermissionCatalogue.PortfolioSummaryView, followUp],
            PortfolioDesignService.PermissionsOf(ColumnTable(Msr("FOLLOW_UP_LATE"), Fld(Budget, PortfolioTableAggregation.Sum))));
    }
}
