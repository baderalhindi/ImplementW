using PMPlatform.Application.Features.Dashboards;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Tests.Unit.Application.Dashboards;

/// <summary>FG-01 §13.4's validation (DSH-CC-06 to -08, -27, -28) and ADR-019's limits on who a dashboard is offered to and what is personalised.</summary>
public sealed class DashboardDefinitionRulesTests
{
    private static readonly BilingualLabel Label = new("لوحة", "Dashboard");

    [Fact]
    public void AValidVersionHasNoIssue() =>
        Assert.Empty(DashboardDefinitionRules.Check(DashboardCode.Portfolio, Content(true, [Audience("R02", true)], Widget("A", DashboardProjections.ProjectLifecycleState, optional: true))));

    /// <summary>BR-DSH-029, SEC-DSH-08: a widget binds a registered projection by code, and nothing else — a table, a query or a formula is no projection.</summary>
    [Theory]
    [InlineData("PROJECT.PROJECT")]
    [InlineData("PROGRESS.PROJECT_HEALTH")]
    [InlineData("SELECT * FROM project.project")]
    public void AnUnregisteredProjectionIsRefused(string code) =>
        Assert.Equal(["widgets[0].sourceProjectionCode|PROJECTION_NOT_REGISTERED"], Issues(DashboardCode.Governance, Content(false, [Audience("R01", true)], Widget("A", code))));

    [Fact]
    public void AProjectionIsBoundOnlyInAContextAndAsAWidgetTypeItSupports()
    {
        Assert.Equal(["widgets[0].sourceProjectionCode|CONTEXT_NOT_SUPPORTED"],
            Issues(DashboardCode.Portfolio, Content(false, [Audience("R02", true)], Widget("A", DashboardProjections.PublishedProgressHistory, DashboardWidgetType.LineTrend))));
        Assert.Equal(["widgets[0].sourceProjectionCode|CONTEXT_NOT_SUPPORTED"],
            Issues(DashboardCode.Project, Content(false, [Audience("R08", true)], Widget("A", DashboardProjections.DefinitionBacklog))));
        Assert.Equal(["widgets[0].widgetType|WIDGET_TYPE_NOT_SUPPORTED"],
            Issues(DashboardCode.Project, Content(false, [Audience("R08", true)], Widget("A", DashboardProjections.FinancialPosition, DashboardWidgetType.LineTrend))));
    }

    /// <summary>ADR-019: personalisation is the Portfolio Dashboard's, a widget is optional only where it is allowed, and entity users get the Project Dashboard only.</summary>
    [Fact]
    public void PersonalisationAndTheEntityAudienceFollowAdr019()
    {
        Assert.Equal(["allowsPersonalization|NOT_ALLOWED"], Issues(DashboardCode.Governance, Content(true, [Audience("R01", true)], Widget("A", DashboardProjections.DefinitionBacklog))));
        Assert.Equal(["widgets[0].isOptionalVisibility|NOT_ALLOWED"],
            Issues(DashboardCode.Project, Content(false, [Audience("R04", false)], Widget("A", DashboardProjections.ProjectLifecycleState, optional: true))));
        Assert.Equal(["audience[0].roleCode|AUDIENCE_NOT_PERMITTED"], Issues(DashboardCode.Portfolio, Content(false, [Audience("R08", false)], Widget("A", DashboardProjections.ProjectLifecycleState))));
        Assert.True(DashboardCatalogue.MayOpen(DashboardCode.Project, isExternal: true));
        Assert.False(DashboardCatalogue.MayOpen(DashboardCode.Governance, isExternal: true));
        Assert.False(DashboardCatalogue.MayOpen(DashboardCode.Portfolio, isExternal: true));
    }

    [Fact]
    public void WidgetsAreNamedOnceAndDoNotOverlapAndTheAudienceIsNamedOnce()
    {
        IReadOnlyList<string> issues = Issues(DashboardCode.Portfolio, Content(
            false,
            [Audience("R02", true), Audience("R02", false)],
            Widget("A", DashboardProjections.ProjectLifecycleState, column: 1, span: 6),
            Widget("A", DashboardProjections.ProjectHealthStatus, column: 6, span: 3),
            Widget("B", DashboardProjections.ScheduleHealthStatus, column: 9, span: 4)));

        Assert.Equal(["audience[1].roleCode|DUPLICATE", "widgets[1].code|DUPLICATE", "widgets[1].layoutColumn|LAYOUT_OVERLAP"], issues);
        Assert.Equal(["audience|REQUIRED", "widgets|REQUIRED"], Issues(DashboardCode.Portfolio, Content(false, [])));
    }

    [Fact]
    public void ADefaultLandingAnotherPublishedDashboardHoldsIsRefusedAtPublication()
    {
        DashboardDefinitionContent content = Content(false, [Audience("R02", true), Audience("R08", true), Audience("R06", false)], Widget("A", DashboardProjections.ProjectLifecycleState));

        Assert.Equal(["audience[0].isDefaultLanding|DEFAULT_LANDING_TAKEN"],
            DashboardDefinitionRules.CheckLandings(content, new HashSet<string>(["R02", "R06"])).Select(i => $"{i.Field}|{i.Code}"));
    }

    /// <summary>ADR-006: three dashboards, each read in its own context, and every registered projection supports at least one of them.</summary>
    [Fact]
    public void TheCatalogueIsThreeDashboardsAndEveryProjectionHasAPlace()
    {
        Assert.Equal([DashboardCode.Portfolio, DashboardCode.Project, DashboardCode.Governance], DashboardCatalogue.Codes);
        Assert.Equal(DashboardContextKind.Project, DashboardCatalogue.ContextOf(DashboardCode.Project));
        Assert.Equal(DashboardContextKind.Portfolio, DashboardCatalogue.ContextOf(DashboardCode.Governance));
        Assert.Equal(DashboardProjections.All.Count, DashboardProjections.All.Select(p => p.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(DashboardProjections.All, p =>
        {
            Assert.NotEmpty(p.Contexts);
            Assert.NotEmpty(p.WidgetTypes);
            Assert.Matches("^[A-Z][A-Z_]*\\.[A-Z][A-Z_]*$", p.Code);
        });
    }

    private static IReadOnlyList<string> Issues(DashboardCode code, DashboardDefinitionContent content) =>
        [.. DashboardDefinitionRules.Check(code, content).Select(i => $"{i.Field}|{i.Code}")];

    private static DashboardDefinitionContent Content(bool personalizable, IReadOnlyList<DashboardAudienceInput> audience, params DashboardWidgetInput[] widgets) =>
        new(Label, null, personalizable, audience, widgets);

    private static DashboardAudienceInput Audience(string role, bool landing) => new(role, landing);

    private static DashboardWidgetInput Widget(
        string code, string projection, DashboardWidgetType type = DashboardWidgetType.MetricCard, bool optional = false, short column = 1, short span = 12) =>
        new(code, Label, type, projection, null, optional, 1, column, span);
}
