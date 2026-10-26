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

    /// <summary>
    /// TBC-RPT-20 as TASK-072 enables it: a view shared with a role is read and run by that role's holders and by no one else, each under their own
    /// authorisation — the configuration transfers, the authorisation does not — and only its owner changes it, its sharing with it.
    /// </summary>
    [Fact]
    public async Task AViewSharedWithARoleIsReadAndRunByItsHoldersEachAsTheyMaySee()
    {
        using HttpClient client = host.Api.CreateClient();
        (string owner, string holder) = ((await client.SignInOrFailAsync(2)).AccessToken, (await client.SignInOrFailAsync(3)).AccessToken);
        Guid ownDepartment = await host.ReportProjectAsync(state: "SUSPENDED", title: "Shared view, holder's department");
        Guid otherDepartment = await host.ReportProjectAsync(state: "SUSPENDED", departmentId: ReportTestHost.OtherDepartmentId, title: "Shared view, other department");

        // Shared with R07 only: local.r03, who holds R03, learns nothing of it.
        using HttpResponseMessage created = await client.PostAsync(ReportDriver.SavedViews, owner, Shared(["R07"]));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        JsonObject view = await created.ReadObjectAsync();
        Guid id = AdministrationApi.IdOf(view);
        Assert.Equal(["R07"], view["sharedWithRoleCodes"]!.AsArray().Select(c => c!.GetValue<string>()));
        Assert.Equal(ReportDriver.Person(2).ToString(), view["ownerUserId"]!.GetValue<string>());
        await AssertHiddenFromAsync(client, holder, id);

        // Shared with R03: its holder reads it — its owner named, the other roles it is shared with not — lists it, and runs it as they may see.
        string etag;
        using (HttpResponseMessage reshared = await client.PutAsync($"{ReportDriver.SavedViews}/{id}", owner, Shared(["R03"]), AdministrationApi.ETagOf(created)))
        {
            Assert.True(reshared.StatusCode == HttpStatusCode.OK, await reshared.Content.ReadAsStringAsync());
            etag = AdministrationApi.ETagOf(reshared);
        }

        using (HttpResponseMessage read = await client.GetAsync($"{ReportDriver.SavedViews}/{id}", holder))
        {
            JsonObject shared = await read.ReadObjectAsync();
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal(ReportDriver.Person(2).ToString(), shared["ownerUserId"]!.GetValue<string>());
            Assert.Empty(shared["sharedWithRoleCodes"]!.AsArray());
        }

        using (HttpResponseMessage listed = await client.GetAsync($"{ReportDriver.SavedViews}?pageSize=100", holder))
        {
            JsonNode? summary = (await listed.ReadObjectAsync())["items"]!.AsArray().Single(v => v!["id"]!.GetValue<string>() == id.ToString());
            Assert.Equal(("VALID", ReportDriver.Person(2).ToString()), (summary!["compatibility"]!.GetValue<string>(), summary["ownerUserId"]!.GetValue<string>()));
        }

        // The configuration transfers, the authorisation does not: the owner's run reaches both departments, the holder's their own only.
        IReadOnlyList<Guid> ownersRows = (await RunViewOrFailAsync(client, owner, id)).ProjectIds();
        IReadOnlyList<Guid> holdersRows = (await RunViewOrFailAsync(client, holder, id)).ProjectIds();
        Assert.Contains(ownDepartment, ownersRows);
        Assert.Contains(otherDepartment, ownersRows);
        Assert.Contains(ownDepartment, holdersRows);
        Assert.DoesNotContain(otherDepartment, holdersRows);

        // The holder may not change or delete it: neither a write nor a delete reaches the owner's view.
        using (HttpResponseMessage edit = await client.PutAsync($"{ReportDriver.SavedViews}/{id}", holder, Shared(["R03", "R07"]), etag))
        {
            Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        }

        using (HttpResponseMessage delete = await client.SendAsync(HttpMethod.Delete, $"{ReportDriver.SavedViews}/{id}", holder))
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        // The owner stops sharing it: from then on it is theirs alone again.
        using (HttpResponseMessage unshared = await client.PutAsync($"{ReportDriver.SavedViews}/{id}", owner, Shared([]), etag))
        {
            Assert.True(unshared.StatusCode == HttpStatusCode.OK, await unshared.Content.ReadAsStringAsync());
            Assert.Empty((await unshared.ReadObjectAsync())["sharedWithRoleCodes"]!.AsArray());
        }

        await AssertHiddenFromAsync(client, holder, id);
    }

    /// <summary>ADR-019: a view is shared only with the saved-view roles R02, R03 and R07, each once.</summary>
    [Fact]
    public async Task AViewIsSharedOnlyWithTheSavedViewRolesEachOnce()
    {
        using HttpClient client = host.Api.CreateClient();
        string owner = (await client.SignInOrFailAsync(2)).AccessToken;
        foreach ((string[] roles, string error) in new[]
                 {
                     (new[] { "R04" }, "sharedWithRoleCodes[0] NOT_ALLOWED"),
                     (["R01"], "sharedWithRoleCodes[0] NOT_ALLOWED"),
                     (["R08"], "sharedWithRoleCodes[0] NOT_ALLOWED"),
                     (["R07", "R07"], "sharedWithRoleCodes[1] NOT_ALLOWED"),
                 })
        {
            using HttpResponseMessage refused = await client.PostAsync(ReportDriver.SavedViews, owner, Shared(roles));
            (HttpStatusCode status, string? code, string[] errors) = await refused.RefusalAsync();
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "REPORT_SAVED_VIEW_NOT_ALLOWED", error), (status, code, Assert.Single(errors)));
        }

        using HttpResponseMessage malformed = await client.PostAsync(ReportDriver.SavedViews, owner, Shared(["r02; DROP"]));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    private static object Shared(string[] roles) => new
    {
        name = new { text = "Suspended projects, shared", language = "en" },
        viewType = "EXPLORER_COMPOSITION",
        columns = new[] { new { sourceEntityCode = "PROJECT", fieldCode = "FORMAL_PROJECT_ID", sortDirection = (string?)"ASC" } },
        filters = new[] { new { sourceEntityCode = "PROJECT_LIFECYCLE_STATE", fieldCode = "LIFECYCLE_STATE", @operator = "EQ", value = "SUSPENDED" } },
        sharedWithRoleCodes = roles,
    };

    /// <summary>The view does not exist for the person: not read, not run, not listed (R-47).</summary>
    private static async Task AssertHiddenFromAsync(HttpClient client, string token, Guid id)
    {
        foreach (Func<Task<HttpResponseMessage>> attempt in new Func<Task<HttpResponseMessage>>[]
                 {
                     () => client.GetAsync($"{ReportDriver.SavedViews}/{id}", token),
                     () => client.PostAsync($"{ReportDriver.SavedViews}/{id}/run", token),
                 })
        {
            using HttpResponseMessage refused = await attempt();
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        }

        using HttpResponseMessage listed = await client.GetAsync($"{ReportDriver.SavedViews}?pageSize=100", token);
        Assert.DoesNotContain((await listed.ReadObjectAsync())["items"]!.AsArray(), v => v!["id"]!.GetValue<string>() == id.ToString());
    }

    private static async Task<JsonObject> RunViewOrFailAsync(HttpClient client, string token, Guid id)
    {
        using HttpResponseMessage run = await client.PostAsync($"{ReportDriver.SavedViews}/{id}/run?pageSize=200", token);
        Assert.True(run.StatusCode == HttpStatusCode.OK, $"run: {(int)run.StatusCode} {await run.Content.ReadAsStringAsync()}");
        return await run.ReadObjectAsync();
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
