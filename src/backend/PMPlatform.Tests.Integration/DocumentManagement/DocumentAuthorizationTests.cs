using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.DocumentManagement;

/// <summary>
/// TASK-037 acceptance criterion 3 — access to a document requires an explicit authorization check, not merely project
/// membership — and the participation amendment: ADR-013's entity Project Manager uploads, views and sees version history
/// on their own project within classification rules, and nowhere else.
/// </summary>
[Collection(DocumentSuite.Name)]
public sealed class DocumentAuthorizationTests(DocumentTestHost host)
{
    [Fact]
    public async Task ProjectMembershipAloneOpensNoDocument()
    {
        using HttpClient client = host.Api.CreateClient();
        string controller = (await client.SignInOrFailAsync(2)).AccessToken;
        string member = (await client.SignInOrFailAsync(6)).AccessToken;
        string departmental = (await client.SignInOrFailAsync(3)).AccessToken;
        (Guid onEntityProject, Guid entityVersion) = await client.UploadOrFailAsync(controller, DocumentDriver.Text($"a {Guid.NewGuid()}"), DocumentTestHost.EntityProjectId);
        (Guid onOtherDepartment, _) = await client.UploadOrFailAsync(controller, DocumentDriver.Text($"b {Guid.NewGuid()}"), DocumentTestHost.OtherDepartmentProjectId);
        await host.Api.ScanAsync();

        // local.r06 is on the entity project but holds no document permission: every document call is refused.
        foreach (string path in new[]
                 {
                     DocumentDriver.Documents, $"{DocumentDriver.Documents}/{onEntityProject}", $"{DocumentDriver.Documents}/{onEntityProject}/versions",
                     $"{DocumentDriver.Documents}/{onEntityProject}/versions/{entityVersion}/content",
                 })
        {
            await AssertAnswerAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED", client.GetAsync(path, member));
        }

        // local.r03 is on the other department's project too, but their document grant is DEPT: that project's document is
        // not found, as if it did not exist (R-47), and the refusal is audited. Their own department's document opens.
        await AssertAnswerAsync(HttpStatusCode.NotFound, "NOT_FOUND", client.GetAsync($"{DocumentDriver.Documents}/{onOtherDepartment}", departmental));
        using (HttpResponseMessage listed = await client.GetAsync($"{DocumentDriver.Documents}?projectId={DocumentTestHost.OtherDepartmentProjectId}", departmental))
        {
            Assert.Equal(0, (await listed.ReadObjectAsync())["totalCount"]!.GetValue<int>());
        }

        Assert.NotEmpty(await host.Database.QueryAsync($"""
            SELECT id::text FROM audit_activity.audit_event
            WHERE event_class = 'AUTHORIZATION_DENIAL' AND actor_user_id = '{IdentityDatabase.UserId(3)}' AND scope_project_id = '{DocumentTestHost.OtherDepartmentProjectId}'
            """));
        using HttpResponseMessage own = await client.GetAsync($"{DocumentDriver.Documents}/{onEntityProject}", departmental);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task TheEntityProjectManagerUploadsViewsAndSeesHistoryOnTheirOwnProjectOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(8)).AccessToken;
        string controller = (await client.SignInOrFailAsync(2)).AccessToken;

        // Upload, a second version, and the version history, on their own project.
        (Guid documentId, _) = await client.UploadOrFailAsync(manager, DocumentDriver.Text($"progress photo log {Guid.NewGuid()}"), DocumentTestHost.EntityProjectId);
        using (HttpResponseMessage added = await client.AddVersionAsync(manager, documentId, DocumentDriver.Text($"v2 {Guid.NewGuid()}")))
        {
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }

        using (HttpResponseMessage history = await client.GetAsync($"{DocumentDriver.Documents}/{documentId}/versions", manager))
        {
            JsonArray versions = await history.ReadArrayAsync();
            Assert.Equal([2, 1], versions.Select(v => v!["versionNo"]!.GetValue<int>()));
            Assert.All(versions, v => Assert.Equal(IdentityDatabase.UserId(8), v!["uploadedByUserId"]!.GetValue<string>()));
        }

        using (HttpResponseMessage detail = await client.GetAsync($"{DocumentDriver.Documents}/{documentId}", manager))
        {
            Assert.Equal(IdentityDatabase.UserId(8), (await detail.ReadObjectAsync())["ownerUserId"]!.GetValue<string>());
        }

        // Nowhere else: another project of their own entity, another entity's project, another department's, the library.
        foreach (Guid? elsewhere in new Guid?[] { DocumentTestHost.SecondEntityProjectId, DocumentTestHost.OtherEntityProjectId, DocumentTestHost.OtherDepartmentProjectId, null })
        {
            using HttpResponseMessage refused = await client.UploadAsync(manager, DocumentDriver.Text("x"), elsewhere);
            Assert.Equal((HttpStatusCode.Forbidden, "PERMISSION_DENIED"), (refused.StatusCode, await refused.CodeOfAsync()));
        }

        (Guid sameEntityOtherProject, _) = await client.UploadOrFailAsync(controller, DocumentDriver.Text($"c {Guid.NewGuid()}"), DocumentTestHost.SecondEntityProjectId);
        await AssertAnswerAsync(HttpStatusCode.NotFound, "NOT_FOUND", client.GetAsync($"{DocumentDriver.Documents}/{sameEntityOtherProject}", manager));
        using (HttpResponseMessage mine = await client.GetAsync($"{DocumentDriver.Documents}?pageSize=200", manager))
        {
            JsonArray items = (await mine.ReadObjectAsync())["items"]!.AsArray();
            Assert.Contains(items, d => AdministrationApi.IdOf(d!.AsObject()) == documentId);
            Assert.All(items, d => Assert.Equal(DocumentTestHost.EntityProjectId.ToString(), d!["projectId"]!.GetValue<string>()));
        }

        // Upload, view and history only: managing is not theirs.
        await AssertAnswerAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED", client.PostAsync($"{DocumentDriver.Documents}/{documentId}/archive", manager));
    }

    /// <summary>Within classification rules: nothing above the document permissions' clearance (here INTERNAL) is uploaded or seen.</summary>
    [Fact]
    public async Task NoOneReachesADocumentAboveTheirClearance()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(8)).AccessToken;
        string controller = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid confidential = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO document_management.document (id, title, title_lang, document_type_item_id, data_classification_item_id, project_id, owner_user_id, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{confidential}', 'Confidential assessment', 'en', '{DocumentTestHost.DocumentTypeId}', '{DocumentTestHost.Confidential}', '{DocumentTestHost.EntityProjectId}',
                    '{IdentityDatabase.UserId(2)}', 'ACTIVE', now(), '{IdentityDatabase.UserId(2)}', now(), '{IdentityDatabase.UserId(2)}')
            """);

        foreach (string token in new[] { manager, controller })
        {
            using HttpResponseMessage refused = await client.UploadAsync(token, DocumentDriver.Text("x"), DocumentTestHost.EntityProjectId, DocumentTestHost.Confidential);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            await AssertAnswerAsync(HttpStatusCode.NotFound, "NOT_FOUND", client.GetAsync($"{DocumentDriver.Documents}/{confidential}", token));
            using HttpResponseMessage listed = await client.GetAsync($"{DocumentDriver.Documents}?projectId={DocumentTestHost.EntityProjectId}&pageSize=200", token);
            Assert.DoesNotContain((await listed.ReadObjectAsync())["items"]!.AsArray(), d => AdministrationApi.IdOf(d!.AsObject()) == confidential);
        }
    }

    /// <summary>
    /// The list is filtered in SQL from the engine's record scope (TASK-037 D-6): for every person and every document in the
    /// database, the document is listed if and only if the person may open it.
    /// </summary>
    [Fact]
    public async Task EachPersonsListIsExactlyTheDocumentsTheyMayOpen()
    {
        using HttpClient client = host.Api.CreateClient();
        string controller = (await client.SignInOrFailAsync(2)).AccessToken;
        await client.UploadOrFailAsync(controller, DocumentDriver.Text($"library {Guid.NewGuid()}"));
        foreach (Guid project in new[] { DocumentTestHost.EntityProjectId, DocumentTestHost.SecondEntityProjectId, DocumentTestHost.OtherDepartmentProjectId, DocumentTestHost.OtherEntityProjectId })
        {
            await client.UploadOrFailAsync(controller, DocumentDriver.Text($"doc {Guid.NewGuid()}"), project);
        }

        IReadOnlyList<string> all = await host.Database.QueryAsync("SELECT id::text FROM document_management.document");
        foreach (int person in new[] { 2, 3, 8 })
        {
            string token = (await client.SignInOrFailAsync(person)).AccessToken;
            using HttpResponseMessage list = await client.GetAsync($"{DocumentDriver.Documents}?pageSize=200", token);
            JsonObject page = await list.ReadObjectAsync();
            Assert.True(page["totalCount"]!.GetValue<int>() <= 200, "the check needs every document on one page");
            HashSet<string> listed = [.. page["items"]!.AsArray().Select(d => d!["id"]!.GetValue<string>())];

            foreach (string id in all)
            {
                using HttpResponseMessage opened = await client.GetAsync($"{DocumentDriver.Documents}/{id}", token);
                Assert.True(listed.Contains(id) == (opened.StatusCode == HttpStatusCode.OK), $"local.r0{person}, document {id}: listed {listed.Contains(id)}, opened {opened.StatusCode}");
            }
        }
    }

    private static async Task AssertAnswerAsync(HttpStatusCode status, string code, Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        Assert.Equal((status, code), (response.StatusCode, await response.CodeOfAsync()));
    }
}
