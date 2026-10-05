using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
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
        Assert.Equal(9, built["paths"]!.AsObject().Count);
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
        Dictionary<string, IReadOnlyList<string>> checkedOperations = [];

        async Task<JsonObject?> CheckAsync(string method, string template, HttpResponseMessage response)
        {
            string name = $"{method} {template}";
            string status = ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            JsonObject documented = Assert.IsType<JsonObject>(document.Operation(template, method)?["responses"]?[status]);
            JsonObject? body = response.Content.Headers.ContentLength == 0 ? null : await response.ReadObjectAsync();
            checkedOperations[name] = body is null
                ? documented["content"] is null ? [] : [$"{name} {status}: no body, documented with one"]
                : document.Violations(body, documented["content"]?["application/json"]?["schema"], $"{name} {status}");
            return body;
        }

        string item = $"{ProjectDriver.Projects}/{{projectId}}";
        using HttpResponseMessage created = await client.PostAsync(ProjectDriver.Projects, sessions.Entity, host.Registration());
        Guid projectId = AdministrationApi.IdOf((await CheckAsync("POST", ProjectDriver.Projects, created))!);
        string path = $"{ProjectDriver.Projects}/{projectId}";

        using HttpResponseMessage read = await client.GetAsync(path, sessions.Entity);
        await CheckAsync("GET", item, read);
        using HttpResponseMessage edited = await client.PutAsync(path, sessions.Entity, host.Registration(change: r => r["latitude"] = 24.7136), AdministrationApi.ETagOf(read));
        await CheckAsync("PUT", item, edited);

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
            await CheckAsync("POST", $"{item}/{command}", response);
        }

        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);
        await ScheduleFixture.ActiveBaselineAsync(host.Database, projectId);
        using HttpResponseMessage activated = await client.PostAsync($"{path}/activate", sessions.Approver);
        Assert.Equal("ACTIVE", (await CheckAsync("POST", $"{item}/activate", activated))!.Status());

        using HttpResponseMessage listed = await client.GetAsync($"{ProjectDriver.Projects}?q={(await host.RowAsync(projectId))["formal_project_id"]}", sessions.Approver);
        Assert.Single((await CheckAsync("GET", ProjectDriver.Projects, listed))!["items"]!.AsArray());

        Guid draft = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Entity, host.Registration()));
        using HttpResponseMessage deleted = await client.DeleteProjectAsync(draft, sessions.Entity);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await CheckAsync("DELETE", item, deleted);

        Assert.Equal(document.Operations(Tag).Select(o => o.Name).Order(), checkedOperations.Keys.Order());
        Assert.Empty(checkedOperations.Values.SelectMany(v => v));
    }

    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return await OpenApiDocument.BuiltAsync(client);
    }
}
