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
/// catalogue it offers, rules 1 to 6 and the aggregations' arithmetic.
/// </summary>
public sealed class PortfolioDesignRulesTests
{
    private static PortfolioVisualInput Visual(
        PortfolioVisualType type = PortfolioVisualType.Bar, PortfolioGroupBy groupBy = PortfolioGroupBy.Sector, short column = 0, short row = 0, short width = 6,
        short height = 4, params PortfolioMeasure[] measures) =>
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

    [Fact]
    public void TheStandardDesignPlacesEverySection() =>
        Assert.Equal(Enum.GetValues<PortfolioSection>(), PortfolioDesignService.StandardDesign.Select(v => v.Section).OfType<PortfolioSection>().Order());

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

    /// <summary>Only project-grain fields of a type a visual calculates with are offered, each keyed by its projection and field, with its type's aggregations.</summary>
    [Fact]
    public void TheCatalogueOffersProjectGrainFieldsToCalculateWith()
    {
        Assert.All(Catalogue, f => Assert.Matches(new Regex("^[A-Z][A-Z_]*\\.[A-Z][A-Z_]*\\.[A-Z][A-Z0-9_]*$"), f.Key));
        Assert.DoesNotContain(Catalogue, f => f.ProjectionCode == DashboardProjections.PublishedProgressHistory);
        Assert.DoesNotContain(Catalogue, f => f.FieldCode == "ACTUAL_PERCENT_OVERRIDDEN");
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
            PortfolioDesignService.Aggregate(aggregation, ["100.25", null, "250.00", "0.25", null]));

    [Theory]
    [InlineData(PortfolioFieldAggregation.Sum)]
    [InlineData(PortfolioFieldAggregation.Average)]
    [InlineData(PortfolioFieldAggregation.Min)]
    [InlineData(PortfolioFieldAggregation.Max)]
    public void NothingRevealedIsNotAvailable(PortfolioFieldAggregation aggregation)
    {
        Assert.Null(PortfolioDesignService.Aggregate(aggregation, [null, null]));
        Assert.Equal(0m, PortfolioDesignService.Aggregate(PortfolioFieldAggregation.Count, [null, null]));
    }

    [Fact]
    public void AStateIsCounted() => Assert.Equal(2m, PortfolioDesignService.Aggregate(PortfolioFieldAggregation.Count, ["RED", "GREEN", null]));

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
}
