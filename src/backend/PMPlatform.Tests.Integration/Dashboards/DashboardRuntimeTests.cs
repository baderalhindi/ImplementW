using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Dashboards;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Dashboards;

/// <summary>
/// TASK-069's acceptance criteria against the API: every widget carries its semantic state and as-of; a missing or stale source is explicitly
/// UNKNOWN or STALE, never 0 or a default; and the catalogue is the three dashboards of ADR-006. Beside them, what keeps a dashboard from
/// becoming a back door: a role selects a dashboard and grants nothing, an entity user sees its own project and nothing internal, scope is
/// applied before anything is counted, and one failing source does not blank the rest.
/// </summary>
[Collection(DashboardSuite.Name)]
public sealed class DashboardRuntimeTests(DashboardTestHost host)
{
    private static readonly string[] SemanticStates = ["CURRENT_LIVE", "PUBLISHED_OFFICIAL", "HISTORICAL_SNAPSHOT"];

    /// <summary>Acceptance criterion 3 and the workbook's check: the published count is ADR-006's three, and the database refuses a fourth.</summary>
    [Fact]
    public async Task TheCatalogueIsTheThreeDashboardsOfAdr006()
    {
        Assert.Equal(["GOVERNANCE", "PORTFOLIO", "PROJECT"],
            await host.Database.QueryAsync("SELECT code FROM dashboards.dashboard_definition WHERE lifecycle_state = 'PUBLISHED' ORDER BY code"));

        string? refused = null;
        try
        {
            await host.Database.ExecuteAsync($"""
                INSERT INTO dashboards.dashboard_definition (id, code, version_no, name_ar, name_en, allows_personalization, lifecycle_state, created_at, created_by, updated_at, updated_by)
                VALUES ('{Guid.NewGuid()}', 'EXECUTIVE', 1, 'تنفيذي', 'Executive', false, 'DRAFT', now(), '{DashboardDriver.Person(1)}', now(), '{DashboardDriver.Person(1)}')
                """);
        }
        catch (Npgsql.PostgresException exception)
        {
            refused = exception.ConstraintName;
        }

        Assert.Equal("ck_dashboard_definition_code", refused);

        // Every seeded widget is bound to a registered projection, in a context and a type it supports.
        IReadOnlyList<string> bindings = await host.Database.QueryAsync("""
            SELECT d.code || '|' || w.source_projection_code || '|' || w.widget_type FROM dashboards.dashboard_widget w
            JOIN dashboards.dashboard_definition d ON d.id = w.dashboard_definition_id WHERE d.lifecycle_state = 'PUBLISHED'
            """);
        Assert.Equal(26, bindings.Count);
        Assert.All(bindings, binding =>
        {
            string[] parts = binding.Split('|');
            ProjectionContract projection = DashboardProjections.Find(parts[1]) ?? throw new Xunit.Sdk.XunitException($"{binding}: not registered");
            Assert.True(projection.Supports(DashboardCatalogue.ContextOf(Enum.Parse<Domain.Dashboards.DashboardCode>(parts[0], ignoreCase: true))), binding);
            Assert.Contains(projection.WidgetTypes, t => string.Equals(t.ToString(), parts[2].Replace("_", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>Blueprint §20.2 and ADR-019: each role lands on its dashboard, and an entity user is offered the Project Dashboard and nothing further.</summary>
    [Fact]
    public async Task EachRoleLandsOnItsDashboardAndAnEntityUserIsOfferedTheProjectDashboardOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        foreach ((int person, string[] offered, string landing) in new[]
                 {
                     (1, new[] { "GOVERNANCE" }, "GOVERNANCE"),
                     (2, ["PORTFOLIO", "PROJECT", "GOVERNANCE"], "PORTFOLIO"),
                     (3, ["PORTFOLIO", "PROJECT", "GOVERNANCE"], "PORTFOLIO"),
                     (6, ["PORTFOLIO", "PROJECT"], "PORTFOLIO"),
                     (8, ["PROJECT"], "PROJECT"),
                 })
        {
            string token = (await client.SignInOrFailAsync(person)).AccessToken;
            using HttpResponseMessage response = await client.GetAsync(DashboardDriver.Dashboards, token);
            JsonArray items = (await response.ReadObjectAsync())["items"]!.AsArray();

            Assert.Equal(offered.Order(StringComparer.Ordinal), items.Select(i => i!["code"]!.GetValue<string>()).Order(StringComparer.Ordinal));
            Assert.Equal([landing], items.Where(i => i!["isDefaultLanding"]!.GetValue<bool>()).Select(i => i!["code"]!.GetValue<string>()));
        }

        // R04 is in the Governance audience, but an entity's Project Manager is an entity user (ADR-013): the dashboard does not exist for them.
        string entityUser = (await client.SignInOrFailAsync(8)).AccessToken;
        foreach (string code in new[] { "PORTFOLIO", "GOVERNANCE" })
        {
            using HttpResponseMessage refused = await client.GetAsync($"{DashboardDriver.Dashboards}/{code}", entityUser);
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        }
    }

    /// <summary>Acceptance criterion 1: every widget's payload carries its semantic state and as-of, and live and published values are never merged.</summary>
    [Fact]
    public async Task EveryWidgetCarriesItsSemanticStateAndAsOfAndLiveAndPublishedStayApart()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid projectId = await host.DashboardProjectAsync();
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow.AddDays(-2);
        DateTimeOffset computedAt = DateTimeOffset.UtcNow.AddHours(-1);
        Guid closed = await host.ReportingPeriodAsync(projectId, DashboardDriver.Today.AddDays(-14), DashboardDriver.Today.AddDays(-7), "CLOSED");
        await host.ReportingPeriodAsync(projectId, DashboardDriver.Today.AddDays(-7), DashboardDriver.Today.AddDays(3), "OPEN");
        await host.PublishedSnapshotAsync(projectId, closed, "GREEN", "AMBER", 40m, 50m, publishedAt);
        await host.LiveHealthAsync(projectId, "AMBER", computedAt);
        await host.ScheduleHealthAsync(projectId, "RED", 12, computedAt);
        await host.OpenRiskAsync(projectId);
        await host.PublishedFinancialsAsync(projectId, closed, "MEASURED", "GREEN", "1000000.00", "400000.00", "990000.00", DashboardDriver.Today.AddDays(-3));

        JsonObject dashboard = await client.DashboardOrFailAsync(portfolio, "PROJECT", $"?projectId={projectId}");

        Assert.Equal(12, dashboard["widgets"]!.AsArray().Count);
        Assert.All(dashboard["widgets"]!.AsArray().Select(w => w!.AsObject()), widget =>
        {
            Assert.Contains(widget.Meta("semanticState"), SemanticStates);
            Assert.True(widget["projection"]!["asOf"] is not null || widget.Reason() is not null, $"{widget["code"]}: neither an as-of nor a reason");
            Assert.False(string.IsNullOrEmpty(widget["projectionVersion"]!.GetValue<string>()));
        });

        JsonObject live = dashboard.Widget("CURRENT_HEALTH");
        JsonObject official = dashboard.Widget("PUBLISHED_HEALTH");
        Assert.Equal(("CURRENT_LIVE", "FRESH", "AMBER"), (live.Meta("semanticState"), live.Meta("freshness"), live["data"]!["state"]!.GetValue<string>()));
        Assert.Equal(("PUBLISHED_OFFICIAL", "FRESH", "GREEN"), (official.Meta("semanticState"), official.Meta("freshness"), official["data"]!["state"]!.GetValue<string>()));
        Assert.Equal(publishedAt.ToUnixTimeMilliseconds(), official["projection"]!["asOf"]!.GetValue<DateTimeOffset>().ToUnixTimeMilliseconds());
        Assert.Equal(computedAt.ToUnixTimeMilliseconds(), live["projection"]!["asOf"]!.GetValue<DateTimeOffset>().ToUnixTimeMilliseconds());

        Assert.Equal("AMBER", dashboard.Widget("PUBLISHED_SCHEDULE")["data"]!["state"]!.GetValue<string>());
        Assert.Equal(("RED", "12"), (dashboard.Widget("SCHEDULE_HEALTH")["data"]!["state"]!.GetValue<string>(), dashboard.Widget("SCHEDULE_HEALTH").Figure("FINISH_VARIANCE_DAYS")));
        Assert.Equal(("40", "false"), (dashboard.Widget("PUBLISHED_PROGRESS").Figure("ACTUAL_PERCENT")?.TrimEnd('0').TrimEnd('.'), dashboard.Widget("PUBLISHED_PROGRESS").Figure("ACTUAL_PERCENT_OVERRIDDEN")));
        Assert.Equal("HISTORICAL_SNAPSHOT", dashboard.Widget("PROGRESS_TREND").Meta("semanticState"));
        Assert.Single(dashboard.Widget("PROGRESS_TREND")["data"]!["series"]!.AsArray());
        Assert.Equal(new Dictionary<string, int> { ["NOT_ASSESSED"] = 1 }, dashboard.Widget("RISK_EXPOSURE").Buckets());
        Assert.Equal("UP_TO_DATE", dashboard.Widget("REPORTING_COMPLETENESS")["data"]!["state"]!.GetValue<string>());

        JsonObject money = dashboard.Widget("PUBLISHED_FINANCIALS");
        Assert.Equal(("PUBLISHED_OFFICIAL", "1000000.00", "GREEN"), (money.Meta("semanticState"), money.Figure("APPROVED_BUDGET"), money["data"]!["state"]!.GetValue<string>()));
        Assert.Equal(DashboardDriver.Today.AddDays(-3), DateOnly.FromDateTime(money["projection"]!["asOf"]!.GetValue<DateTimeOffset>().UtcDateTime));

        // No KPI is assigned to the project: there is nothing to report, which is not 0.
        Assert.Equal(("UNKNOWN", "NOT_APPLICABLE"), (dashboard.Widget("KPI_CONDITION").Meta("freshness"), dashboard.Widget("KPI_CONDITION").Reason()));
        Assert.Null(dashboard.Widget("KPI_CONDITION")["data"]);
    }

    /// <summary>Acceptance criterion 2: a source that holds nothing renders UNKNOWN with its reason and no value; an aggregate says what it left out.</summary>
    [Fact]
    public async Task AMissingSourceIsUnknownAndNeverZero()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid projectId = await host.DashboardProjectAsync();

        JsonObject dashboard = await client.DashboardOrFailAsync(portfolio, "PROJECT", $"?projectId={projectId}");

        foreach (string code in new[] { "CURRENT_HEALTH", "PUBLISHED_HEALTH", "PUBLISHED_PROGRESS", "PUBLISHED_SCHEDULE", "SCHEDULE_HEALTH", "PUBLISHED_FINANCIALS", "PROGRESS_TREND", "REPORTING_COMPLETENESS" })
        {
            JsonObject widget = dashboard.Widget(code);
            Assert.Equal(("UNKNOWN", "MISSING", "NONE"), (widget.Meta("freshness"), widget.Reason(), widget.Meta("coverage")));
            Assert.Null(widget["data"]);
            Assert.Null(widget["projection"]!["asOf"]);
        }

        // Over a population: a project without a value is excluded and counted as missing, never as a zero or a colour.
        Guid isolated = await host.DashboardProjectAsync(departmentId: DashboardTestHost.OtherDepartmentId);
        Guid cycle = await host.ReportingPeriodAsync(isolated, DashboardDriver.Today.AddDays(-14), DashboardDriver.Today.AddDays(-7), "CLOSED");
        await host.PublishedSnapshotAsync(isolated, cycle, "RED", null, 10m, 20m, DateTimeOffset.UtcNow.AddDays(-1));
        Guid silent = await host.DashboardProjectAsync(departmentId: DashboardTestHost.OtherDepartmentId);
        JsonObject portfolioView = await client.DashboardOrFailAsync(portfolio, "PORTFOLIO", $"?departmentId={DashboardTestHost.OtherDepartmentId}");
        JsonObject health = portfolioView.Widget("PUBLISHED_HEALTH");
        Assert.Equal("PARTIAL", health.Meta("coverage"));
        Assert.Equal(1, health.Buckets()["RED"]);
        Assert.DoesNotContain("GREEN", health.Buckets().Keys);
        JsonObject coverage = health["coverageDetail"]!.AsObject();
        Assert.True(coverage["excludedCount"]!.GetValue<int>() >= 1, $"{silent}: not excluded");
        Assert.Contains(coverage["exclusions"]!.AsArray(), e => e!["reason"]!.GetValue<string>() == "MISSING");
        Assert.Equal(coverage["eligibleCount"]!.GetValue<int>(), coverage["includedCount"]!.GetValue<int>() + coverage["excludedCount"]!.GetValue<int>());
    }

    /// <summary>
    /// The workbook's validation check: force a source projection to go stale and confirm the payload marks it STALE rather than showing the last
    /// good value unlabelled. WF-02's own rule: a later period past due unpublished. WF-14's own rule: a period reported STALE.
    /// </summary>
    [Fact]
    public async Task AStaleSourceIsMarkedStaleAndKeepsItsOriginalAsOf()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid projectId = await host.DashboardProjectAsync();
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow.AddDays(-20);
        Guid closed = await host.ReportingPeriodAsync(projectId, DashboardDriver.Today.AddDays(-28), DashboardDriver.Today.AddDays(-21), "CLOSED");
        await host.PublishedSnapshotAsync(projectId, closed, "GREEN", "GREEN", 60m, 60m, publishedAt);
        JsonObject fresh = await client.DashboardOrFailAsync(portfolio, "PROJECT", $"?projectId={projectId}");
        Assert.Equal("FRESH", fresh.Widget("PUBLISHED_HEALTH").Meta("freshness"));

        // The next period falls due and nothing is published for it.
        await host.ReportingPeriodAsync(projectId, DashboardDriver.Today.AddDays(-21), DashboardDriver.Today.AddDays(-3), "OPEN");
        await host.PublishedFinancialsAsync(projectId, closed, "STALE", "UNKNOWN", null, null, null, DashboardDriver.Today.AddDays(-21));
        JsonObject stale = await client.DashboardOrFailAsync(portfolio, "PROJECT", $"?projectId={projectId}");

        JsonObject health = stale.Widget("PUBLISHED_HEALTH");
        Assert.Equal(("STALE", "GREEN"), (health.Meta("freshness"), health["data"]!["state"]!.GetValue<string>()));
        Assert.Equal(publishedAt.ToUnixTimeMilliseconds(), health["projection"]!["asOf"]!.GetValue<DateTimeOffset>().ToUnixTimeMilliseconds());
        Assert.Equal("STALE", stale.Widget("PROGRESS_TREND").Meta("freshness"));
        Assert.Equal(("OVERDUE", "1"), (stale.Widget("REPORTING_COMPLETENESS")["data"]!["state"]!.GetValue<string>(), stale.Widget("REPORTING_COMPLETENESS").Figure("OVERDUE_PERIODS")));

        // WF-14 reported the period STALE: the widget says so, and carries no figure rather than a 0.00.
        JsonObject money = stale.Widget("PUBLISHED_FINANCIALS");
        Assert.Equal(("STALE", "UNKNOWN"), (money.Meta("freshness"), money["data"]!["state"]!.GetValue<string>()));
        Assert.Null(money.Figure("ACTUAL_EXPENDITURE_TO_DATE"));
        Assert.Null(money.Figure("FORECAST_AT_COMPLETION"));
    }

    /// <summary>BR-DSH-040: one source failing answers SOURCE_UNAVAILABLE for its widget, and every other widget still renders.</summary>
    [Fact]
    public async Task AFailingSourceIsUnavailableAndTheOtherWidgetsStillRender()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid projectId = await host.DashboardProjectAsync();
        host.KpiFailure.IsOn = true;
        try
        {
            JsonObject dashboard = await client.DashboardOrFailAsync(portfolio, "PROJECT", $"?projectId={projectId}");
            Assert.Equal(("UNKNOWN", "SOURCE_UNAVAILABLE"), (dashboard.Widget("KPI_CONDITION").Meta("freshness"), dashboard.Widget("KPI_CONDITION").Reason()));
            Assert.Null(dashboard.Widget("KPI_CONDITION")["data"]);
            Assert.Equal(("FRESH", "ACTIVE"), (dashboard.Widget("LIFECYCLE_STATE").Meta("freshness"), dashboard.Widget("LIFECYCLE_STATE")["data"]!["state"]!.GetValue<string>()));
        }
        finally
        {
            host.KpiFailure.IsOn = false;
        }
    }

    /// <summary>
    /// DSH-008 (ADR-013, ADR-019): an entity user's Project Dashboard is its own project's, with progress, schedule status and health, and every
    /// internal widget restricted; another entity's project does not exist for it (R-47, DSH-CC-21).
    /// </summary>
    [Fact]
    public async Task AnEntityUserSeesItsOwnProjectsProgressScheduleStatusAndHealthAndNothingInternal()
    {
        using HttpClient client = host.Api.CreateClient();
        string entityUser = (await client.SignInOrFailAsync(8)).AccessToken;
        Guid own = await host.DashboardProjectAsync();
        Guid cycle = await host.ReportingPeriodAsync(own, DashboardDriver.Today.AddDays(-14), DashboardDriver.Today.AddDays(-7), "CLOSED");
        await host.PublishedSnapshotAsync(own, cycle, "AMBER", "RED", 30m, 45m, DateTimeOffset.UtcNow.AddDays(-1));
        await host.LiveHealthAsync(own, "AMBER", DateTimeOffset.UtcNow.AddHours(-2));
        await host.ScheduleHealthAsync(own, "RED", 20, DateTimeOffset.UtcNow);
        await host.OpenRiskAsync(own);

        JsonObject dashboard = await client.DashboardOrFailAsync(entityUser, "PROJECT", $"?projectId={own}");

        Assert.Equal("AMBER", dashboard.Widget("PUBLISHED_HEALTH")["data"]!["state"]!.GetValue<string>());
        Assert.Equal("RED", dashboard.Widget("PUBLISHED_SCHEDULE")["data"]!["state"]!.GetValue<string>());
        Assert.Equal("AMBER", dashboard.Widget("CURRENT_HEALTH")["data"]!["state"]!.GetValue<string>());
        foreach (string code in new[] { "SCHEDULE_HEALTH", "RISK_EXPOSURE" })
        {
            JsonObject widget = dashboard.Widget(code);
            Assert.Equal(("UNKNOWN", "RESTRICTED"), (widget.Meta("freshness"), widget.Reason()));
            Assert.Null(widget["data"]);
            Assert.Null(widget["drillTargetScreenId"]);
        }

        Guid otherEntity = await host.DashboardProjectAsync(entityId: DashboardTestHost.OtherEntityId);
        using HttpResponseMessage hidden = await client.GetAsync($"{DashboardDriver.Dashboards}/PROJECT?projectId={otherEntity}", entityUser);
        using HttpResponseMessage nonexistent = await client.GetAsync($"{DashboardDriver.Dashboards}/PROJECT?projectId={Guid.NewGuid()}", entityUser);
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (hidden.StatusCode, nonexistent.StatusCode));
    }

    /// <summary>DSH-CC-04, -17, -18: scope decides the population before anything is counted, and the client cannot widen it.</summary>
    [Fact]
    public async Task ScopeDecidesThePopulationBeforeAnythingIsCountedAndTheClientCannotWidenIt()
    {
        using HttpClient client = host.Api.CreateClient();
        (string portfolio, string department, string viewer) =
            ((await client.SignInOrFailAsync(2)).AccessToken, (await client.SignInOrFailAsync(3)).AccessToken, (await client.SignInOrFailAsync(6)).AccessToken);
        await host.DashboardProjectAsync();
        await host.DashboardProjectAsync(departmentId: DashboardTestHost.OtherDepartmentId);

        // R03 is DEPT-scoped to the test department: the other is not a value it may know, and asking for it is refused, not answered empty.
        JsonObject own = await client.DashboardOrFailAsync(department, "PORTFOLIO");
        Assert.Equal([DashboardTestHost.DepartmentId.ToString()], own["departmentOptions"]!.AsArray().Select(d => d!.GetValue<string>()));
        using HttpResponseMessage widened = await client.GetAsync($"{DashboardDriver.Dashboards}/PORTFOLIO?departmentId={DashboardTestHost.OtherDepartmentId}", department);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DASHBOARD_FILTER_VALUE_UNAUTHORIZED"), await widened.DashboardRefusalAsync());

        // R03 holds no schedule, risk, financial or KPI view: those widgets are restricted, whatever R02 could see of the same projects.
        Assert.Equal("RESTRICTED", own.Widget("SCHEDULE_HEALTH").Reason());
        Assert.Equal("FRESH", (await client.DashboardOrFailAsync(portfolio, "PORTFOLIO")).Widget("PROJECTS_BY_LIFECYCLE").Meta("freshness"));

        // R06 holds no grant: the Viewer's dashboard opens, and every widget is restricted with nothing disclosed.
        JsonObject nothing = await client.DashboardOrFailAsync(viewer, "PORTFOLIO");
        Assert.Empty(nothing["departmentOptions"]!.AsArray());
        Assert.All(nothing["widgets"]!.AsArray().Select(w => w!.AsObject()), w =>
        {
            Assert.Equal("RESTRICTED", w.Reason());
            Assert.Null(w["data"]);
        });

        // The Project Dashboard is bound to a project; the others take none (DSH-CC-18). An unknown dashboard does not exist.
        using HttpResponseMessage unbound = await client.GetAsync($"{DashboardDriver.Dashboards}/PROJECT", portfolio);
        using HttpResponseMessage bound = await client.GetAsync($"{DashboardDriver.Dashboards}/PORTFOLIO?projectId={Guid.NewGuid()}", portfolio);
        using HttpResponseMessage unknown = await client.GetAsync($"{DashboardDriver.Dashboards}/EXECUTIVE", portfolio);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DASHBOARD_CONTEXT_INVALID"), await unbound.DashboardRefusalAsync());
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DASHBOARD_CONTEXT_INVALID"), await bound.DashboardRefusalAsync());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    /// <summary>ADR-019: R02, R03 and R07 hide and order the Portfolio Dashboard's optional widgets; nothing else is personalisable, and a reset restores the governed layout.</summary>
    [Fact]
    public async Task OnlyThePortfolioDashboardsOptionalWidgetsArePersonalisedAndAResetRestoresTheGovernedLayout()
    {
        using HttpClient client = host.Api.CreateClient();
        (string portfolio, string viewer) = ((await client.SignInOrFailAsync(2)).AccessToken, (await client.SignInOrFailAsync(6)).AccessToken);
        object hideCurrentHealth = new { widgets = new[] { new { widgetCode = "CURRENT_HEALTH", isHidden = true, sortOrder = (short?)2 } } };

        using HttpResponseMessage personalised = await client.PostAsync($"{DashboardDriver.Dashboards}/PORTFOLIO/personalize", portfolio, hideCurrentHealth);
        Assert.Equal(HttpStatusCode.OK, personalised.StatusCode);
        JsonObject view = await client.DashboardOrFailAsync(portfolio, "PORTFOLIO");
        Assert.Equal((true, 2), (view.Widget("CURRENT_HEALTH")["isHidden"]!.GetValue<bool>(), view.Widget("CURRENT_HEALTH")["personalSortOrder"]!.GetValue<int>()));
        Assert.False(view.Widget("SCHEDULE_HEALTH")["isHidden"]!.GetValue<bool>());

        // A governed widget, another dashboard, and a person without the permission are refused.
        using HttpResponseMessage governed = await client.PostAsync($"{DashboardDriver.Dashboards}/PORTFOLIO/personalize", portfolio,
            new { widgets = new[] { new { widgetCode = "PROJECTS_BY_LIFECYCLE", isHidden = true } } });
        using HttpResponseMessage elsewhere = await client.PostAsync($"{DashboardDriver.Dashboards}/GOVERNANCE/personalize", portfolio, hideCurrentHealth);
        using HttpResponseMessage unpermitted = await client.PostAsync($"{DashboardDriver.Dashboards}/PORTFOLIO/personalize", viewer, hideCurrentHealth);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DASHBOARD_PERSONALIZATION_INVALID"), await governed.DashboardRefusalAsync());
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DASHBOARD_PERSONALIZATION_INVALID"), await elsewhere.DashboardRefusalAsync());
        Assert.Equal(HttpStatusCode.Forbidden, unpermitted.StatusCode);

        using HttpResponseMessage reset = await client.PostAsync($"{DashboardDriver.Dashboards}/PORTFOLIO/reset-personalization", portfolio);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.False((await client.DashboardOrFailAsync(portfolio, "PORTFOLIO")).Widget("CURRENT_HEALTH")["isHidden"]!.GetValue<bool>());
        Assert.Equal(["Dashboards.PersonalizationChanged", "Dashboards.PersonalizationReset"], await host.Database.QueryAsync($"""
            SELECT event_type FROM audit_activity.audit_event
            WHERE subject_module = 'Dashboards' AND subject_type = 'UserDashboardPreference' AND actor_user_id = '{DashboardDriver.Person(2)}' ORDER BY occurred_at, id
            """));
    }
}
