using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Insights;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Tests.Unit.Application.Project;

/// <summary>The 17 Nov 2026 dashboard designer: what a saved design may hold, and the permission each grouping is read under.</summary>
public sealed class PortfolioDesignRulesTests
{
    private static PortfolioVisualInput Visual(
        PortfolioVisualType type = PortfolioVisualType.Bar, PortfolioGroupBy groupBy = PortfolioGroupBy.Sector, short column = 0, short row = 0, short width = 6,
        short height = 4, params PortfolioMeasure[] measures) =>
        new(new BilingualLabel("المشاريع حسب القطاع", "Projects by sector"), type, measures.Length == 0 ? [PortfolioMeasure.Projects] : measures, groupBy, column, row, width, height);

    private static FieldIssue? Issue(params PortfolioVisualInput[] visuals) => PortfolioDesignService.Issue(new PortfolioDesignInput(visuals));

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
}
