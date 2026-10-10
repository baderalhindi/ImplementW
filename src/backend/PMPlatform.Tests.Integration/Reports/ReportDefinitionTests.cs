using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Reports;

/// <summary>
/// ADM-037 under the governed lifecycle (FG-02 §11): a version is authored, reviewed and published by three people; it names registered projections
/// and fields only, and offers a report only to the roles ADR-006 and ADR-013 allow; publishing replaces the version in force, so the catalogue
/// stays ten; a saved view the new version no longer fits is reported incompatible, never reinterpreted; and the database holds it whatever writes.
/// </summary>
[Collection(ReportDefinitionSuite.Name)]
public sealed class ReportDefinitionTests(ReportTestHost host)
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
            "SELECT id::text FROM reports.report_definition WHERE code = 'PROJECT_REGISTER' AND lifecycle_state = 'PUBLISHED'")));

        // A view saved against version 1, filtering on its lifecycle-state parameter.
        Guid viewId;
        using (HttpResponseMessage saved = await client.PostAsync(ReportDriver.SavedViews, reviewer, new
        {
            name = new { text = "Suspended", language = "en" },
            viewType = "REPORT_PARAMETERS",
            reportCode = "PROJECT_REGISTER",
            parameters = new[] { new { code = "LIFECYCLE_STATE", value = "SUSPENDED" } },
        }))
        {
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            viewId = AdministrationApi.IdOf(await saved.ReadObjectAsync());
        }

        using HttpResponseMessage created = await client.PostAsync(ReportDriver.Definitions, author, new { code = "PROJECT_REGISTER" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonObject draft = await created.ReadObjectAsync();
        Guid id = AdministrationApi.IdOf(draft);
        Assert.Equal((2, "DRAFT"), (draft["versionNo"]!.GetValue<int>(), draft["lifecycleState"]!.GetValue<string>()));
        using HttpResponseMessage second = await client.PostAsync(ReportDriver.Definitions, author, new { code = "PROJECT_REGISTER" });
        Assert.Equal((HttpStatusCode.Conflict, "REPORT_VERSION_OPEN"), ((await second.RefusalAsync()).Status, (await second.RefusalAsync()).Code));

        // BR-RPT-046 at validation: a field the register does not have, and a role the report may not be offered to, are refused, each by name and
        // by its place in the stored version (the audience is held in role order, so R01 is first).
        JsonObject invalid = Content(draft);
        invalid["audienceRoleCodes"]!.AsArray().Add("R01");
        invalid["columns"]!.AsArray().Add(new JsonObject
        {
            ["sourceEntityCode"] = "PROJECT",
            ["fieldCode"] = "BUDGET",
            ["label"] = new JsonObject { ["ar"] = "ميزانية", ["en"] = "Budget" },
            ["isDefaultVisible"] = true,
        });
        using (HttpResponseMessage put = await PutAsync(client, author, id, invalid))
        {
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        using (HttpResponseMessage refused = await client.PostAsync($"{ReportDriver.Definitions}/{id}/validate", reviewer))
        {
            (HttpStatusCode status, string? code, string[] errors) = await refused.RefusalAsync();
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "REPORT_DEFINITION_INVALID"), (status, code));
            Assert.Equal(
                ["audienceRoleCodes[0] AUDIENCE_NOT_PERMITTED", $"columns[{invalid["columns"]!.AsArray().Count - 1}] PROJECTION_NOT_REGISTERED"],
                errors.Order(StringComparer.Ordinal));
        }

        // Version 2 drops the lifecycle-state parameter.
        JsonObject valid = Content(draft);
        JsonArray kept = [.. valid["parameters"]!.AsArray().Where(p => p!["code"]!.GetValue<string>() != "LIFECYCLE_STATE").Select(p => p!.DeepClone())];
        valid["parameters"] = kept;
        using (HttpResponseMessage put = await PutAsync(client, author, id, valid))
        {
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        // Three people: the author validates nothing, the reviewer publishes nothing.
        using (HttpResponseMessage selfReview = await client.PostAsync($"{ReportDriver.Definitions}/{id}/validate", author))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "REPORT_SEPARATION_OF_DUTIES"), ((await selfReview.RefusalAsync()).Status, (await selfReview.RefusalAsync()).Code));
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{ReportDriver.Definitions}/{id}/validate", reviewer)).StatusCode);
        using (HttpResponseMessage editValidated = await PutAsync(client, author, id, valid))
        {
            Assert.Equal(HttpStatusCode.Conflict, editValidated.StatusCode);
        }

        using (HttpResponseMessage reviewerPublishes = await client.PostAsync($"{ReportDriver.Definitions}/{id}/publish", reviewer))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "REPORT_SEPARATION_OF_DUTIES"), ((await reviewerPublishes.RefusalAsync()).Status, (await reviewerPublishes.RefusalAsync()).Code));
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{ReportDriver.Definitions}/{id}/publish", publisher)).StatusCode);

        // The version it replaced is retired in the same save; the catalogue is still ten.
        Assert.Equal(["PROJECT_REGISTER|1|RETIRED", "PROJECT_REGISTER|2|PUBLISHED"], await host.Database.QueryAsync(
            "SELECT code || '|' || version_no || '|' || lifecycle_state FROM reports.report_definition WHERE code = 'PROJECT_REGISTER' ORDER BY version_no"));
        Assert.Equal("10", Assert.Single(await host.Database.QueryAsync("SELECT count(*)::text FROM reports.report_definition WHERE lifecycle_state = 'PUBLISHED'")));

        // SAV-012: the view saved against version 1 no longer fits; it says why and does not run.
        using (HttpResponseMessage view = await client.GetAsync($"{ReportDriver.SavedViews}/{viewId}", reviewer))
        {
            JsonObject detail = await view.ReadObjectAsync();
            Assert.Equal("INCOMPATIBLE", detail["compatibility"]!.GetValue<string>());
            Assert.Equal(["parameters[0].code|NOT_FOUND"], detail["issues"]!.AsArray().Select(i => $"{i!["field"]}|{i["code"]}"));
        }

        using (HttpResponseMessage run = await client.PostAsync($"{ReportDriver.SavedViews}/{viewId}/run", reviewer))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "REPORT_SAVED_VIEW_INCOMPATIBLE"), ((await run.RefusalAsync()).Status, (await run.RefusalAsync()).Code));
        }

        // A PUBLISHED version is replaced, never retired alone, and never edited.
        using (HttpResponseMessage retire = await client.PostAsync($"{ReportDriver.Definitions}/{id}/retire", publisher))
        {
            Assert.Equal((HttpStatusCode.Conflict, "REPORT_RETIREMENT_NOT_PERMITTED"), ((await retire.RefusalAsync()).Status, (await retire.RefusalAsync()).Code));
        }

        Assert.Equal(["Reports.DefinitionCreated", "Reports.DefinitionChanged", "Reports.DefinitionChanged", "Reports.DefinitionValidated", "Reports.DefinitionPublished"],
            await AuditEventsAsync(id));
        Assert.Equal(["Reports.DefinitionRetired"], await AuditEventsAsync(inForce));
    }

    /// <summary>An abandoned DRAFT is retired by anyone who manages configuration; configuring reports is refused to whoever does not.</summary>
    [Fact]
    public async Task AnAbandonedDraftIsRetiredAndConfigurationIsForItsManagersOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        string author = (await client.SignInOrFailAsync(1)).AccessToken;
        using HttpResponseMessage created = await client.PostAsync(ReportDriver.Definitions, author, new { code = "RISK_ISSUE" });
        Guid id = AdministrationApi.IdOf(await created.ReadObjectAsync());
        using HttpResponseMessage retired = await client.PostAsync($"{ReportDriver.Definitions}/{id}/retire", author);
        Assert.Equal("RETIRED", (await retired.ReadObjectAsync())["lifecycleState"]!.GetValue<string>());

        using HttpResponseMessage refused = await client.PostAsync(ReportDriver.Definitions, (await client.SignInOrFailAsync(8)).AccessToken, new { code = "RISK_ISSUE" });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    /// <summary>The guard of TASK-071 migration 3, for any writer: a PUBLISHED report does not change, nothing is deleted or truncated, and two versions of one report are never both in force.</summary>
    [Fact]
    public async Task TheDatabaseHoldsTheGovernanceWhateverWrites()
    {
        string published = Assert.Single(await host.Database.QueryAsync(
            "SELECT id::text FROM reports.report_definition WHERE code = 'PORTFOLIO_SUMMARY' AND lifecycle_state = 'PUBLISHED'"));
        string writer = ReportDriver.Person(1).ToString();

        Assert.Equal(RestrictViolation, await RefusedAsync($"UPDATE reports.report_definition SET name_en = 'Renamed' WHERE id = '{published}'"));
        Assert.Equal(RestrictViolation, await RefusedAsync($"DELETE FROM reports.report_definition WHERE id = '{published}'"));
        Assert.Equal(RestrictViolation, await RefusedAsync($"UPDATE reports.report_column SET is_default_visible = NOT is_default_visible WHERE report_definition_id = '{published}'"));
        Assert.Equal(RestrictViolation, await RefusedAsync($"DELETE FROM reports.report_audience_role WHERE report_definition_id = '{published}'"));
        Assert.Equal(RestrictViolation, await RefusedAsync("TRUNCATE reports.report_job CASCADE"));
        Assert.Equal("ck_report_definition_born", await RefusedAsync($"""
            INSERT INTO reports.report_definition (id, code, version_no, name_ar, name_en, audience_family, primary_projection_code, allows_saved_views, lifecycle_state,
                                                   created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), 'PORTFOLIO_SUMMARY', 99, 'ملخص', 'Summary', 'PORTFOLIO', 'PROJECT.LIFECYCLE_STATE', true, 'PUBLISHED', now(), '{writer}', now(), '{writer}')
            """));
        Assert.Equal("ck_report_definition_primary_projection_code", await RefusedAsync($"""
            INSERT INTO reports.report_definition (id, code, version_no, name_ar, name_en, audience_family, primary_projection_code, allows_saved_views, lifecycle_state,
                                                   created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), 'PORTFOLIO_SUMMARY', 98, 'ملخص', 'Summary', 'PORTFOLIO', 'x; DROP TABLE project.project', true, 'DRAFT', now(), '{writer}', now(), '{writer}')
            """));
    }

    /// <summary>A version's content as a PUT body: the detail's writable members, copied.</summary>
    private static JsonObject Content(JsonObject detail)
    {
        JsonObject content = [];
        foreach (string member in new[] { "name", "description", "audienceFamily", "primaryProjectionCode", "allowsSavedViews", "audienceRoleCodes", "parameters", "columns" })
        {
            content[member] = detail[member]?.DeepClone();
        }

        return content;
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string token, Guid id, JsonObject body)
    {
        using HttpResponseMessage current = await client.GetAsync($"{ReportDriver.Definitions}/{id}", token);
        return await client.PutAsync($"{ReportDriver.Definitions}/{id}", token, body, AdministrationApi.ETagOf(current));
    }

    private Task<IReadOnlyList<string>> AuditEventsAsync(Guid definitionId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'Reports' AND subject_id = '{definitionId}' ORDER BY occurred_at, id");

    private async Task<string?> RefusedAsync(string sql)
    {
        try
        {
            await host.Database.ExecuteAsync(sql);
            return null;
        }
        catch (Npgsql.PostgresException exception)
        {
            return exception.ConstraintName ?? exception.SqlState;
        }
    }
}
