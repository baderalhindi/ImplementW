using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Persistence;

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
    /// The TASK-009 lint's findings on the Project surface that no module can clear yet, because the API emits no R-52
    /// extension, no <c>default</c> response and no response header (record F-1). Any other finding fails.
    /// </summary>
    private static readonly Regex[] OpenPlatformFindings =
    [
        KnownFinding(@"C-4: .*: x-module must equal the module tag"),
        KnownFinding(@"C-5: .*: responses\.default \(ProblemDetails\) is required \(R-53\)"),
        KnownFinding(@"C-7: .*: x-write-class must be 'sensitive' or 'non-sensitive' \(R-35\)"),
        KnownFinding(@"C-7: .*: a command endpoint is always a sensitive write \(R-4, R-35\)"),
        KnownFinding(@"C-8: PUT .*: PUT without a required If-Match header \(R-21\)"),
        KnownFinding(@"C-12: .*: X-Correlation-Id response header not declared \(R-41\)"),
        KnownFinding(@"C-12: .*: Location header not declared \(R-5\)"),
    ];

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
        OpenApiDocument built = await BuiltAsync();
        string document = Path.Combine(AppContext.BaseDirectory, "openapi.v1.generated.json");
        built.Write(document);

        (int exitCode, string output) = await RunAsync("python3", RepositoryFile.Path("docs/architecture/contract-check.py"), document);

        Assert.True(exitCode is 0 or 1, $"contract-check.py did not run: {output}");
        Assert.Contains(" operations, ", output, StringComparison.Ordinal);
        string schemas = string.Join('|', built.Surface(Tag)["schemas"]!.AsObject().Select(s => Regex.Escape(s.Key)));
        Regex projectFinding = new($@"^C-\d+: ([A-Z]+ /api/v1/projects[/: ]|({schemas})[.: ])", RegexOptions.CultureInvariant);
        Assert.DoesNotContain(output.Split('\n'), line => projectFinding.IsMatch(line) && !OpenPlatformFindings.Any(known => known.IsMatch(line)));
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

    /// <summary>The document as the API serves it; with <see cref="OpenApiDocument.UpdateVariable"/> set, also written as the snapshot.</summary>
    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        OpenApiDocument built = await OpenApiDocument.FetchAsync(client);
        if (Environment.GetEnvironmentVariable(OpenApiDocument.UpdateVariable) == "1")
        {
            string snapshot = Path.Combine(Path.GetDirectoryName(RepositoryFile.Path("global.json"))!, OpenApiDocument.SnapshotPath);
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            built.Write(snapshot);
        }

        return built;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string program, params string[] arguments)
    {
        ProcessStartInfo start = new(program) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output + await error);
    }

    private static Regex KnownFinding(string pattern) => new($"^{pattern}$", RegexOptions.CultureInvariant);
}
