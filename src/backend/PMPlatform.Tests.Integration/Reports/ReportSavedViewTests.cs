using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Reports;

/// <summary>
/// MOD-062 and SAV-001 to -012: a saved view is its owner's configuration only — invisible to anyone else, run with the runner's own authorisation,
/// held to the allowlist at save and at run — and an external entity's person saves none (ADR-019: R02, R03 and R07 only).
/// </summary>
[Collection(ReportSuite.Name)]
public sealed class ReportSavedViewTests(ReportTestHost host)
{
    private static readonly object Composition = new
    {
        name = new { text = "Suspended projects", language = "en" },
        viewType = "EXPLORER_COMPOSITION",
        columns = new[]
        {
            new { sourceEntityCode = "PROJECT", fieldCode = "FORMAL_PROJECT_ID", sortDirection = (string?)"ASC" },
            new { sourceEntityCode = "PROJECT", fieldCode = "TITLE", sortDirection = (string?)null },
        },
        filters = new[] { new { sourceEntityCode = "PROJECT_LIFECYCLE_STATE", fieldCode = "LIFECYCLE_STATE", @operator = "EQ", value = "SUSPENDED" } },
    };

    /// <summary>SAV-003, BR-RPT-034: another person learns nothing of a view, not even that it exists, and cannot remove it; its owner reads, runs and deletes it.</summary>
    [Fact]
    public async Task AViewIsItsOwnersAloneAndRunsWithTheRunnersAuthorisation()
    {
        using HttpClient client = host.Api.CreateClient();
        (string owner, string other) = ((await client.SignInOrFailAsync(2)).AccessToken, (await client.SignInOrFailAsync(3)).AccessToken);
        Guid suspended = await host.ReportProjectAsync(state: "SUSPENDED", title: "Saved view project");

        using HttpResponseMessage created = await client.PostAsync(ReportDriver.SavedViews, owner, Composition);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        JsonObject view = await created.ReadObjectAsync();
        Guid id = AdministrationApi.IdOf(view);
        Assert.Equal(("VALID", 0), (view["compatibility"]!.GetValue<string>(), view["issues"]!.AsArray().Count));

        foreach (Func<Task<HttpResponseMessage>> attempt in new Func<Task<HttpResponseMessage>>[]
                 {
                     () => client.GetAsync($"{ReportDriver.SavedViews}/{id}", other),
                     () => client.PostAsync($"{ReportDriver.SavedViews}/{id}/run", other),
                 })
        {
            using HttpResponseMessage refused = await attempt();
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        }

        // R-40: a delete is 204 whether or not the view was the caller's, and removes nothing that was not.
        using (HttpResponseMessage notTheirs = await client.SendAsync(HttpMethod.Delete, $"{ReportDriver.SavedViews}/{id}", other))
        {
            Assert.Equal(HttpStatusCode.NoContent, notTheirs.StatusCode);
        }

        using (HttpResponseMessage kept = await client.GetAsync($"{ReportDriver.SavedViews}/{id}", owner))
        {
            Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        }

        using (HttpResponseMessage listed = await client.GetAsync($"{ReportDriver.SavedViews}?pageSize=100", other))
        {
            Assert.DoesNotContain((await listed.ReadObjectAsync())["items"]!.AsArray(), v => v!["id"]!.GetValue<string>() == id.ToString());
        }

        using (HttpResponseMessage run = await client.PostAsync($"{ReportDriver.SavedViews}/{id}/run?pageSize=200", owner))
        {
            JsonObject result = await run.ReadObjectAsync();
            Assert.Equal(HttpStatusCode.OK, run.StatusCode);
            Assert.Contains(suspended, result.ProjectIds());
            Assert.All(result["items"]!.AsArray(), r => Assert.NotEqual("ACTIVE", r!["title"]?.GetValue<string>()));
        }

        using HttpResponseMessage deleted = await client.SendAsync(HttpMethod.Delete, $"{ReportDriver.SavedViews}/{id}", owner);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using HttpResponseMessage gone = await client.GetAsync($"{ReportDriver.SavedViews}/{id}", owner);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    /// <summary>SAV-005, BR-RPT-031: a view is held to the allowlist when saved; an entity's person may not save one.</summary>
    [Fact]
    public async Task AViewIsHeldToTheAllowlistAndOnlyItsComposersSaveOne()
    {
        using HttpClient client = host.Api.CreateClient();
        string owner = (await client.SignInOrFailAsync(2)).AccessToken;

        using (HttpResponseMessage outside = await client.PostAsync(ReportDriver.SavedViews, owner, new
        {
            name = new { text = "Budgets", language = "en" },
            viewType = "EXPLORER_COMPOSITION",
            columns = new[] { new { sourceEntityCode = "FINANCIAL_KPI_FINANCIAL_POSITION", fieldCode = "APPROVED_BUDGET", sortDirection = (string?)null } },
        }))
        {
            (HttpStatusCode status, string? code, string[] errors) = await outside.RefusalAsync();
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "REPORT_COLUMN_NOT_SUPPORTED"), (status, code));
            Assert.Equal(["columns[0] FIELD_NOT_ALLOWLISTED"], errors);
        }

        using (HttpResponseMessage parameters = await client.PostAsync(ReportDriver.SavedViews, owner, new
        {
            name = new { text = "Register", language = "en" },
            viewType = "REPORT_PARAMETERS",
            reportCode = "PROJECT_REGISTER",
            parameters = new[] { new { code = "LIFECYCLE_STATE", value = "DELETED" } },
        }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "REPORT_PARAMETER_INVALID"), ((await parameters.RefusalAsync()).Status, (await parameters.RefusalAsync()).Code));
        }

        using HttpResponseMessage entity = await client.PostAsync(ReportDriver.SavedViews, (await client.SignInOrFailAsync(8)).AccessToken, Composition);
        Assert.Equal(HttpStatusCode.Forbidden, entity.StatusCode);
    }
}
