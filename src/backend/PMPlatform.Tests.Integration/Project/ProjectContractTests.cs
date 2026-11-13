using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.DataImport;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// TASK-043 contract tests of the WF-01 API, as api-conventions §9 row 5 defines them: the generated document diffed
/// against the committed snapshot for R-10 breaking changes, the document linted against the TASK-009 conventions, and
/// every Project response checked against the schema its operation documents.
/// </summary>
/// <remarks>
/// A change to the Project API is made with the snapshot: run these tests with <c>UPDATE_OPENAPI_SNAPSHOT=1</c>, commit the
/// rewritten <c>docs/api/openapi.v1.json</c>, and, if <see cref="TheProjectApiKeepsEveryPromiseOfTheSnapshot"/> failed
/// first, call the break out in the PR as R-11 requires.
/// </remarks>
[Collection(ProjectSuite.Name)]
public sealed class ProjectContractTests(ProjectTestHost host)
{
    private const string Tag = "Project";

    /// <summary>
    /// The acceptance criterion: a removed operation, status code, response property or enum value, or a changed type,
    /// format or nullability, fails the build. <see cref="OpenApiBreakingChangeTests"/> proves each kind is caught.
    /// </summary>
    [Fact]
    public async Task TheProjectApiKeepsEveryPromiseOfTheSnapshot()
    {
        IReadOnlyList<string> breaks = OpenApiBreakingChanges.Find(OpenApiDocument.Snapshot(), await BuiltAsync(), Tag);

        Assert.True(breaks.Count == 0, $"""
            The Project API breaks its contract (api-conventions R-10) against {OpenApiDocument.SnapshotPath}:
              {string.Join("\n  ", breaks)}
            A break ships only under R-11: rewrite the snapshot ({OpenApiDocument.UpdateVariable}=1) and call the break out in the PR.
            """);
    }

    /// <summary>
    /// The snapshot is what is guarded, so it must be the API as built: an operation or property added without updating it
    /// could later be removed unnoticed.
    /// </summary>
    [Fact]
    public async Task TheSnapshotIsTheProjectApiAsBuilt()
    {
        JsonObject built = (await BuiltAsync()).Surface(Tag);

        Assert.True(JsonNode.DeepEquals(OpenApiDocument.Snapshot().Surface(Tag), built), $"""
            {OpenApiDocument.SnapshotPath} does not describe the Project API as built. Run the Project contract tests with
            {OpenApiDocument.UpdateVariable}=1 and commit the rewritten file with the change.
            """);
        Assert.Equal(33, built["paths"]!.AsObject().Count);
    }

    /// <summary>
    /// TASK-009's lint over the generated document: on the Project operations and the schemas they reach, nothing beyond
    /// the platform-wide findings no module can clear yet.
    /// </summary>
    [Fact]
    public async Task TheProjectApiFollowsTheConventions()
    {
        IReadOnlyList<string> findings = await (await BuiltAsync()).LintAsync(Tag);

        Assert.DoesNotContain(findings, line => !OpenApiDocument.OpenPlatformFindings.Any(known => known.IsMatch(line)));
    }

    /// <summary>
    /// The document describes the wire: every Project operation is called, and each response body has exactly the
    /// properties, JSON types and enum values its documented schema gives (api-conventions R-55's replay, in process).
    /// </summary>
    [Fact]
    public async Task EveryProjectResponseIsWhatItsOperationDocuments()
    {
        OpenApiDocument document = await BuiltAsync();
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        DocumentedResponses responses = new(document);

        string item = $"{ProjectDriver.Projects}/{{projectId}}";
        using HttpResponseMessage created = await client.PostAsync(ProjectDriver.Projects, sessions.Entity, host.Registration());
        Guid projectId = AdministrationApi.IdOf((await responses.CheckAsync("POST", ProjectDriver.Projects, created))!);
        string path = $"{ProjectDriver.Projects}/{projectId}";

        using HttpResponseMessage read = await client.GetAsync(path, sessions.Entity);
        await responses.CheckAsync("GET", item, read);
        using HttpResponseMessage edited = await client.PutAsync(path, sessions.Entity, host.Registration(change: r => r["latitude"] = 24.7136), AdministrationApi.ETagOf(read));
        await responses.CheckAsync("PUT", item, edited);

        foreach ((string command, string token, object? body) in new (string, string, object?)[]
                 {
                     ("submit", sessions.Entity, new { projectManagerUserId = ProjectDriver.Person(8) }),
                     ("withdraw", sessions.Entity, null),
                     ("submit", sessions.Entity, new { projectManagerUserId = ProjectDriver.Person(8) }),
                     ("start-review", sessions.Reviewer, null),
                 })
        {
            using HttpResponseMessage response = await client.PostAsync($"{path}/{command}", token, body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await responses.CheckAsync("POST", $"{item}/{command}", response);
        }

        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);
        await ScheduleFixture.ActiveBaselineAsync(host.Database, projectId);
        using HttpResponseMessage activated = await client.PostAsync($"{path}/activate", sessions.Approver);
        Assert.Equal("ACTIVE", (await responses.CheckAsync("POST", $"{item}/activate", activated))!.Status());

        using HttpResponseMessage listed = await client.GetAsync($"{ProjectDriver.Projects}?q={(await host.RowAsync(projectId))["formal_project_id"]}", sessions.Approver);
        Assert.Single((await responses.CheckAsync("GET", ProjectDriver.Projects, listed))!["items"]!.AsArray());

        // The 2 Nov 2026 review: the follow-up of an approved project, its notes and the Follow-up list (R3-3, R3-5).
        using HttpResponseMessage current = await client.GetAsync(path, sessions.Approver);
        using HttpResponseMessage followedUp = await client.PostAsync($"{path}/update-follow-up", sessions.Approver,
            new { currentObstacle = new { text = "تأخر التصاريح", language = "ar" }, declaredPlannedPercent = 40, declaredActualPercent = 25, note = new { text = "متابعة", language = "ar" } },
            AdministrationApi.ETagOf(current));
        Assert.Equal("STALLED", (await responses.CheckAsync("POST", $"{item}/update-follow-up", followedUp))!["indicators"]!["onPlan"]!.GetValue<string>());
        using HttpResponseMessage notes = await client.GetAsync($"/api/v1/project-follow-up-notes?projectId={projectId}", sessions.Approver);
        Assert.Single((await responses.CheckAsync("GET", "/api/v1/project-follow-up-notes", notes))!["items"]!.AsArray());
        using HttpResponseMessage followUps = await client.GetAsync("/api/v1/project-follow-ups", sessions.Approver);
        Assert.Contains((await responses.CheckAsync("GET", "/api/v1/project-follow-ups", followUps))!["items"]!.AsArray(), i => i!["projectId"]!.GetValue<string>() == projectId.ToString());

        // Review round 7, item 1: the stage plan of the active project, which has none, set once. The stages are in force only for this step: with
        // them every registration in this suite would need a plan.
        await StageFixture.PublishAsync(host.Database);
        try
        {
            using HttpResponseMessage stages = await client.GetAsync("/api/v1/project-stages", sessions.Approver);
            Assert.Equal(6, (await responses.CheckAsync("GET", "/api/v1/project-stages", stages))!["items"]!.AsArray().Count);
            using HttpResponseMessage plan = await client.GetAsync($"/api/v1/project-stage-plans/{projectId}", sessions.Entity);
            await responses.CheckAsync("GET", "/api/v1/project-stage-plans/{projectId}", plan);
            DateOnly start = DateOnly.Parse((await host.RowAsync(projectId))["planned_start_date"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
            using HttpResponseMessage planned = await client.PutAsync(
                $"/api/v1/project-stage-plans/{projectId}", sessions.Entity, StageFixture.Plan(start), AdministrationApi.ETagOf(plan));
            Assert.Equal(1, (await responses.CheckAsync("PUT", "/api/v1/project-stage-plans/{projectId}", planned))!["revisionNo"]!.GetValue<int>());
        }
        finally
        {
            await StageFixture.WithdrawAsync(host.Database);
        }

        // R3-4: a batch from RPMO's workbook, validated, submitted by local.r03, approved and rolled back by local.r02; a second one returned.
        const string batches = "/api/v1/project-import-batches";
        ProjectImportTests import = new(host);
        using HttpResponseMessage importTemplate = await client.GetAsync($"{batches}/import-template/content", sessions.Approver);
        await responses.CheckAsync("GET", $"{batches}/import-template/content", importTemplate);
        byte[] rpmo = await ProjectImportTests.WorkbookAsync([await import.RowAsync($"CONTRACT-{Guid.NewGuid():N}"[..20], "نشط")]);
        using HttpResponseMessage rpmoValidated = await ProjectImportTests.SendValidationAsync(client, sessions.Reviewer, rpmo);
        await responses.CheckAsync("POST", $"{batches}/validations", rpmoValidated);
        using HttpResponseMessage submittedBatch = await ProjectImportTests.SubmitAsync(client, sessions.Reviewer, rpmo, string.Empty);
        Guid batchId = AdministrationApi.IdOf((await responses.CheckAsync("POST", batches, submittedBatch))!);
        using HttpResponseMessage batchList = await client.GetAsync(batches, sessions.Approver);
        await responses.CheckAsync("GET", batches, batchList);
        using HttpResponseMessage batchRead = await client.GetAsync($"{batches}/{batchId}", sessions.Approver);
        await responses.CheckAsync("GET", $"{batches}/{{batchId}}", batchRead);
        using HttpResponseMessage batchPreview = await client.GetAsync($"/api/v1/project-import-batch-previews/{batchId}", sessions.Approver);
        await responses.CheckAsync("GET", "/api/v1/project-import-batch-previews/{batchId}", batchPreview);
        using HttpResponseMessage batchFile = await client.GetAsync($"{batches}/{batchId}/content", sessions.Approver);
        await responses.CheckAsync("GET", $"{batches}/{{batchId}}/content", batchFile);
        using HttpResponseMessage batchApproved = await ProjectImportTests.PostAsync(client, sessions.Approver, $"{batches}/{batchId}/approve");
        await responses.CheckAsync("POST", $"{batches}/{{batchId}}/approve", batchApproved);
        using HttpResponseMessage batchRolledBack = await ProjectImportTests.PostAsync(client, sessions.Approver, $"{batches}/{batchId}/rollback");
        await responses.CheckAsync("POST", $"{batches}/{{batchId}}/rollback", batchRolledBack);
        byte[] second = await ProjectImportTests.WorkbookAsync([await import.RowAsync($"CONTRACT-{Guid.NewGuid():N}"[..20], "نشط")]);
        using HttpResponseMessage resubmitted = await ProjectImportTests.SubmitAsync(client, sessions.Reviewer, second, string.Empty);
        Guid returnedId = AdministrationApi.IdOf((await resubmitted.ReadObjectAsync())!);
        using HttpResponseMessage batchReturned = await ProjectImportTests.PostAsync(client, sessions.Approver, $"{batches}/{returnedId}/return",
            "{\"reason\":{\"text\":\"أكمل الأسماء\",\"language\":\"ar\"}}");
        await responses.CheckAsync("POST", $"{batches}/{{batchId}}/return", batchReturned);

        // The 2 Nov 2026 review, round 3 follow-up: a background validation read back, and a value request asked for and resolved.
        Guid validationId = Guid.CreateVersion7();
        await host.Database.ExecuteAsync($$"""
            INSERT INTO project.project_import_validation (id, status, file_name, file_content, plan, row_count, result, completed_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{{validationId}}', 'COMPLETED', 'rpmo.xlsx', ''::bytea, '{"mappings":[],"skippedRows":[]}', 0,
                    '{"fileSha256":"","rowCount":0,"createCount":0,"updateCount":0,"skipCount":0,"errorCount":0,"warningCount":0,"submittable":false,"fileIssues":[],"unmatched":[],"rows":[]}',
                    now(), now(), '{{IdentityDatabase.UserId(3)}}', now(), '{{IdentityDatabase.UserId(3)}}');
            """);
        using HttpResponseMessage validationRead = await client.GetAsync($"/api/v1/project-import-validations/{validationId}", sessions.Reviewer);
        await responses.CheckAsync("GET", "/api/v1/project-import-validations/{validationId}", validationRead);
        const string valueRequests = "/api/v1/project-import-value-requests";
        using HttpResponseMessage asked = await ProjectImportTests.PostAsync(client, sessions.Reviewer, valueRequests,
            $"{{\"dimension\":\"SECTOR\",\"sourceText\":\"قطاع {Guid.NewGuid():N}\"}}");
        Guid valueRequestId = AdministrationApi.IdOf((await responses.CheckAsync("POST", valueRequests, asked))!);
        using HttpResponseMessage requestList = await client.GetAsync($"{valueRequests}?status=OPEN", sessions.Reviewer);
        await responses.CheckAsync("GET", valueRequests, requestList);
        string resolver = (await client.SignInOrFailAsync(1)).AccessToken;
        using HttpResponseMessage resolved = await ProjectImportTests.PostAsync(client, resolver, $"{valueRequests}/{valueRequestId}/resolve", "{\"status\":\"DECLINED\"}");
        await responses.CheckAsync("POST", $"{valueRequests}/{{requestId}}/resolve", resolved);

        Guid draft = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Entity, host.Registration()));
        using HttpResponseMessage deleted = await client.DeleteProjectAsync(draft, sessions.Entity);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await responses.CheckAsync("DELETE", item, deleted);

        // TASK-102: the migration template and a validation, as the one holder of DATA_IMPORT calls them.
        string administrator = (await client.SignInOrFailAsync(1)).AccessToken;
        using HttpResponseMessage template = await client.TemplateAsync(ProjectDriver.Projects, administrator);
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        await responses.CheckAsync("GET", $"{ProjectDriver.Projects}/import-template/content", template);
        using HttpResponseMessage validated = await client.UploadAsync(ProjectDriver.Projects, administrator,
            ImportApi.Workbook(["legacy_id", "title"], ["CONTRACT-1", "Contract check"]), commit: false);
        Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        await responses.CheckAsync("POST", $"{ProjectDriver.Projects}/imports", validated);

        // The 5 Nov 2026 review §3: the figures of RPMO's Excel, and a card of a portfolio nobody's project holds.
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;
        using HttpResponseMessage insights = await client.GetAsync("/api/v1/portfolio-insights", manager);
        Assert.Equal(HttpStatusCode.OK, insights.StatusCode);
        await responses.CheckAsync("GET", "/api/v1/portfolio-insights", insights);
        using HttpResponseMessage card = await client.GetAsync($"/api/v1/portfolio-insights/cards?dimension=PORTFOLIO&itemId={Guid.NewGuid()}", manager);
        await responses.CheckAsync("GET", "/api/v1/portfolio-insights/cards", card);

        Assert.Equal(document.Operations(Tag).Select(o => o.Name).Order(), responses.Operations.Order());
        Assert.Empty(responses.Violations);
    }

    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return await OpenApiDocument.BuiltAsync(client);
    }
}
