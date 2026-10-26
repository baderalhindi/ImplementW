using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Dashboards;

/// <summary>
/// ADM-036 under the governed lifecycle (FG-01 §13, DSH-CC-26 to -28): a version is authored, reviewed and published by three people; only a
/// registered projection can be bound and nothing executable can be configured; publishing replaces the version in force, which is never
/// retired alone, so the catalogue stays ADR-006's three; and the database holds all of it whatever writes.
/// </summary>
[Collection(DashboardDefinitionSuite.Name)]
public sealed class DashboardDefinitionTests(DashboardTestHost host)
{
    /// <summary>The SQLSTATE the guard refuses with.</summary>
    private const string RestrictViolation = "23001";

    [Fact]
    public async Task AVersionIsAuthoredReviewedAndPublishedByThreePeopleAndReplacesTheOneInForce()
    {
        using HttpClient client = host.Api.CreateClient();
        (string author, string reviewer, string publisher) =
            ((await client.SignInOrFailAsync(1)).AccessToken, (await client.SignInOrFailAsync(2)).AccessToken, (await client.SignInOrFailAsync(3)).AccessToken);
        Guid inForce = Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            "SELECT id::text FROM dashboards.dashboard_definition WHERE code = 'GOVERNANCE' AND lifecycle_state = 'PUBLISHED'")));

        using HttpResponseMessage created = await client.PostAsync(DashboardDriver.Definitions, author, new { code = "GOVERNANCE" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonObject draft = await created.ReadObjectAsync();
        Guid id = AdministrationApi.IdOf(draft);
        Assert.Equal((2, "DRAFT", 5), (draft["versionNo"]!.GetValue<int>(), draft["lifecycleState"]!.GetValue<string>(), draft["widgets"]!.AsArray().Count));
        using HttpResponseMessage second = await client.PostAsync(DashboardDriver.Definitions, author, new { code = "GOVERNANCE" });
        Assert.Equal((HttpStatusCode.Conflict, "DASHBOARD_VERSION_OPEN"), await second.DashboardRefusalAsync());

        // BR-DSH-030 at the edge: a projection named by anything but a code is malformed, and nothing reaches the module.
        using HttpResponseMessage script = await PutAsync(client, author, id, Content(Widget("ROGUE", "SELECT * FROM project.project")));
        Assert.Equal(HttpStatusCode.BadRequest, script.StatusCode);

        // BR-DSH-029 at validation: a code that names no registered projection, or one this dashboard's context does not support, is refused.
        using HttpResponseMessage unregistered = await PutAsync(client, author, id, Content(Widget("ROGUE", "PROJECT.RAW_TABLE"), Widget("TREND", "PROGRESS.PUBLISHED_PROGRESS_HISTORY", "LINE_TREND", 2)));
        Assert.Equal(HttpStatusCode.OK, unregistered.StatusCode);
        using HttpResponseMessage invalid = await client.PostAsync($"{DashboardDriver.Definitions}/{id}/validate", reviewer);
        JsonObject problem = await invalid.ReadObjectAsync();
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DASHBOARD_DEFINITION_INVALID"), (invalid.StatusCode, problem["code"]!.GetValue<string>()));
        Assert.Equal(["widgets[0].sourceProjectionCode|PROJECTION_NOT_REGISTERED", "widgets[1].sourceProjectionCode|CONTEXT_NOT_SUPPORTED"],
            problem["errors"]!.AsArray().Select(e => $"{e!["field"]}|{e["code"]}").Order(StringComparer.Ordinal));

        using HttpResponseMessage valid = await PutAsync(client, author, id, Content(Widget("REPORTING", "PROGRESS.REPORTING_COMPLETENESS"), Widget("BACKLOG", "DASHBOARDS.DEFINITION_BACKLOG", "STATUS_DISTRIBUTION", 2)));
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        // Three people: the author validates nothing, the reviewer publishes nothing.
        using HttpResponseMessage selfReview = await client.PostAsync($"{DashboardDriver.Definitions}/{id}/validate", author);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DASHBOARD_SEPARATION_OF_DUTIES"), await selfReview.DashboardRefusalAsync());
        using HttpResponseMessage validated = await client.PostAsync($"{DashboardDriver.Definitions}/{id}/validate", reviewer);
        Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        using HttpResponseMessage editValidated = await PutAsync(client, author, id, Content(Widget("REPORTING", "PROGRESS.REPORTING_COMPLETENESS")));
        Assert.Equal(HttpStatusCode.Conflict, editValidated.StatusCode);
        using HttpResponseMessage reviewerPublishes = await client.PostAsync($"{DashboardDriver.Definitions}/{id}/publish", reviewer);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DASHBOARD_SEPARATION_OF_DUTIES"), await reviewerPublishes.DashboardRefusalAsync());
        using HttpResponseMessage published = await client.PostAsync($"{DashboardDriver.Definitions}/{id}/publish", publisher);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        // The version it replaced is retired in the same save; the catalogue is still three, and the runtime reads the new version.
        Assert.Equal(["GOVERNANCE|1|RETIRED", "GOVERNANCE|2|PUBLISHED"], await host.Database.QueryAsync(
            "SELECT code || '|' || version_no || '|' || lifecycle_state FROM dashboards.dashboard_definition WHERE code = 'GOVERNANCE' ORDER BY version_no"));
        Assert.Equal("3", Assert.Single(await host.Database.QueryAsync("SELECT count(*)::text FROM dashboards.dashboard_definition WHERE lifecycle_state = 'PUBLISHED'")));
        JsonObject runtime = await client.DashboardOrFailAsync(author, "GOVERNANCE");
        Assert.Equal((2, 2), (runtime["versionNo"]!.GetValue<int>(), runtime["widgets"]!.AsArray().Count));

        // A PUBLISHED version is replaced, never retired alone, and never edited.
        using HttpResponseMessage retire = await client.PostAsync($"{DashboardDriver.Definitions}/{id}/retire", publisher);
        Assert.Equal((HttpStatusCode.Conflict, "DASHBOARD_RETIREMENT_NOT_PERMITTED"), await retire.DashboardRefusalAsync());
        using HttpResponseMessage editPublished = await PutAsync(client, author, id, Content(Widget("REPORTING", "PROGRESS.REPORTING_COMPLETENESS")));
        Assert.Equal(HttpStatusCode.Conflict, editPublished.StatusCode);

        Assert.Equal(["Dashboards.DefinitionCreated", "Dashboards.DefinitionChanged", "Dashboards.DefinitionChanged", "Dashboards.DefinitionValidated", "Dashboards.DefinitionPublished"],
            await AuditEventsAsync(id));
        Assert.Equal(["Dashboards.DefinitionRetired"], await AuditEventsAsync(inForce));
    }

    /// <summary>Blueprint §20.2 at publication: a role lands by default on one PUBLISHED dashboard.</summary>
    [Fact]
    public async Task ARoleThatAlreadyLandsElsewhereCannotLandOnASecondDashboard()
    {
        using HttpClient client = host.Api.CreateClient();
        (string author, string reviewer, string publisher) =
            ((await client.SignInOrFailAsync(1)).AccessToken, (await client.SignInOrFailAsync(2)).AccessToken, (await client.SignInOrFailAsync(3)).AccessToken);
        using HttpResponseMessage created = await client.PostAsync(DashboardDriver.Definitions, author, new { code = "PROJECT" });
        Guid id = AdministrationApi.IdOf(await created.ReadObjectAsync());
        object content = new
        {
            name = new { ar = "لوحة المشروع", en = "Project Dashboard" },
            allowsPersonalization = false,
            audience = new[] { new { roleCode = "R08", isDefaultLanding = true }, new { roleCode = "R02", isDefaultLanding = true } },
            widgets = new[] { Widget("LIFECYCLE", "PROJECT.LIFECYCLE_STATE", "METRIC_CARD") },
        };
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(client, author, id, content)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{DashboardDriver.Definitions}/{id}/validate", reviewer)).StatusCode);

        using HttpResponseMessage refused = await client.PostAsync($"{DashboardDriver.Definitions}/{id}/publish", publisher);
        JsonObject problem = await refused.ReadObjectAsync();
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "DASHBOARD_DEFINITION_INVALID"), (refused.StatusCode, problem["code"]!.GetValue<string>()));
        Assert.Equal(["audience[0].isDefaultLanding|DEFAULT_LANDING_TAKEN"],
            problem["errors"]!.AsArray().Select(e => $"{e!["field"]}|{e["code"]}"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{DashboardDriver.Definitions}/{id}/retire", author)).StatusCode);
    }

    /// <summary>The guard of TASK-069 migration 3, for any writer: a PUBLISHED dashboard does not change, nothing is deleted, and the catalogue cannot grow.</summary>
    [Fact]
    public async Task TheDatabaseHoldsTheGovernanceWhateverWrites()
    {
        string published = Assert.Single(await host.Database.QueryAsync(
            "SELECT id::text FROM dashboards.dashboard_definition WHERE code = 'PORTFOLIO' AND lifecycle_state = 'PUBLISHED'"));
        string writer = DashboardDriver.Person(1).ToString();

        Assert.Equal(RestrictViolation, await RefusedAsync($"UPDATE dashboards.dashboard_definition SET name_en = 'Renamed' WHERE id = '{published}'"));
        Assert.Equal(RestrictViolation, await RefusedAsync($"DELETE FROM dashboards.dashboard_definition WHERE id = '{published}'"));
        Assert.Equal(RestrictViolation, await RefusedAsync($"UPDATE dashboards.dashboard_widget SET layout_span = 1 WHERE dashboard_definition_id = '{published}'"));
        Assert.Equal(RestrictViolation, await RefusedAsync($"""
            INSERT INTO dashboards.dashboard_widget (id, dashboard_definition_id, code, title_ar, title_en, widget_type, source_projection_code, is_optional_visibility,
                                                     layout_row, layout_column, layout_span, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{published}', 'EXTRA', 'إضافي', 'Extra', 'METRIC_CARD', 'PROJECT.LIFECYCLE_STATE', false, 9, 1, 1, now(), '{writer}', now(), '{writer}')
            """));
        Assert.Equal("ck_dashboard_definition_born", await RefusedAsync($"""
            INSERT INTO dashboards.dashboard_definition (id, code, version_no, name_ar, name_en, allows_personalization, lifecycle_state, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), 'PORTFOLIO', 99, 'محفظة', 'Portfolio', false, 'PUBLISHED', now(), '{writer}', now(), '{writer}')
            """));
        Assert.Equal("ck_dashboard_definition_allows_personalization", await RefusedAsync($"""
            INSERT INTO dashboards.dashboard_definition (id, code, version_no, name_ar, name_en, allows_personalization, lifecycle_state, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), 'GOVERNANCE', 98, 'حوكمة', 'Governance', true, 'DRAFT', now(), '{writer}', now(), '{writer}')
            """));
        Assert.Equal("ck_dashboard_widget_source_projection_code", await RefusedAsync($"""
            INSERT INTO dashboards.dashboard_definition (id, code, version_no, name_ar, name_en, allows_personalization, lifecycle_state, created_at, created_by, updated_at, updated_by)
            VALUES ('00000000-0690-4000-8000-0000000000d1', 'GOVERNANCE', 97, 'حوكمة', 'Governance', false, 'DRAFT', now(), '{writer}', now(), '{writer}');
            INSERT INTO dashboards.dashboard_widget (id, dashboard_definition_id, code, title_ar, title_en, widget_type, source_projection_code, is_optional_visibility,
                                                     layout_row, layout_column, layout_span, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '00000000-0690-4000-8000-0000000000d1', 'ROGUE', 'خطأ', 'Rogue', 'METRIC_CARD', 'x; DROP TABLE project.project', false, 1, 1, 1,
                    now(), '{writer}', now(), '{writer}')
            """, inTransaction: true));
    }

    [Fact]
    public async Task TheProjectionRegisterIsListedForConfigurationOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        using HttpResponseMessage listed = await client.GetAsync($"{DashboardDriver.Projections}?pageSize=50", (await client.SignInOrFailAsync(1)).AccessToken);
        JsonObject page = await listed.ReadObjectAsync();
        // Twelve of TASK-069 and the five counts over WF-04, WF-05, WF-07, WF-08 and WF-09 TASK-071 adds for its reports (edges 48 to 52).
        Assert.Equal(17, page["totalCount"]!.GetValue<int>());
        Assert.Contains(page["items"]!.AsArray(), p => p!["code"]!.GetValue<string>() == "PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT" && p["semanticState"]!.GetValue<string>() == "PUBLISHED_OFFICIAL");

        using HttpResponseMessage refused = await client.GetAsync(DashboardDriver.Projections, (await client.SignInOrFailAsync(6)).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    private static object Widget(string code, string projection, string type = "STATUS_DISTRIBUTION", short row = 1) => new
    {
        code,
        title = new { ar = "عنصر", en = "Widget" },
        widgetType = type,
        sourceProjectionCode = projection,
        isOptionalVisibility = false,
        layoutRow = row,
        layoutColumn = (short)1,
        layoutSpan = (short)12,
    };

    private static object Content(params object[] widgets) => new
    {
        name = new { ar = "لوحة الحوكمة", en = "Governance Dashboard" },
        allowsPersonalization = false,
        audience = new[] { new { roleCode = "R01", isDefaultLanding = true }, new { roleCode = "R04", isDefaultLanding = true } },
        widgets,
    };

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string token, Guid id, object body)
    {
        using HttpResponseMessage current = await client.GetAsync($"{DashboardDriver.Definitions}/{id}", token);
        return await client.PutAsync($"{DashboardDriver.Definitions}/{id}", token, body, AdministrationApi.ETagOf(current));
    }

    private Task<IReadOnlyList<string>> AuditEventsAsync(Guid definitionId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'Dashboards' AND subject_id = '{definitionId}' ORDER BY occurred_at, id");

    /// <summary>The constraint or SQLSTATE a raw writer is refused with, or null; with <paramref name="inTransaction"/>, the statements are one transaction.</summary>
    private async Task<string?> RefusedAsync(string sql, bool inTransaction = false)
    {
        try
        {
            await host.Database.ExecuteAsync(inTransaction ? $"BEGIN; {sql}; COMMIT;" : sql);
            return null;
        }
        catch (Npgsql.PostgresException exception)
        {
            return exception.ConstraintName ?? exception.SqlState;
        }
    }
}
