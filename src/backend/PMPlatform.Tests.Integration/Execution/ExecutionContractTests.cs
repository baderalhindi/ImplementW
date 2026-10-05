using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Project;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Execution;

/// <summary>
/// TASK-054's contract half (api-conventions §9 row 5 (b)) for the execution domain: the WF-02, WF-03, WF-04, WF-05 and WF-14
/// APIs against the committed snapshot, as TASK-043 does for WF-01 (project-lifecycle-contract-tests.md F-4).
/// </summary>
/// <remarks>
/// A change to one of these APIs is made with the snapshot: run these tests with <c>UPDATE_OPENAPI_SNAPSHOT=1</c>, commit the
/// rewritten <c>docs/api/openapi.v1.json</c>, and, if <see cref="EachExecutionApiKeepsEveryPromiseOfTheSnapshot"/> failed first,
/// call the break out in the PR as R-11 requires.
/// </remarks>
[Collection(ScheduleSuite.Name)]
public sealed class ExecutionContractTests(ScheduleTestHost host)
{
    /// <summary>Each execution module's tag and the number of operations it carries, so an operation added or dropped is seen.</summary>
    public static TheoryData<string, int> Modules => new()
    {
        { "Progress", 11 },
        { "Schedule", 25 },
        { "ProjectTask", 15 },
        { "Milestone", 9 },
        { "FinancialKpi", 46 },
    };

    public static TheoryData<string> Tags => [.. Modules.Select(m => (string)m[0])];

    /// <summary>
    /// The lint's findings on these surfaces beyond the platform's (<see cref="OpenApiDocument.OpenPlatformFindings"/>), each
    /// recorded with its owner (execution-domain-contract-tests.md F-4). Any other finding fails; one cleared must be removed here.
    /// </summary>
    private static readonly string[] RecordedFindings =
    [
        "C-2: /api/v1/project-baselines/{baselineId}/baseline-activities: a segment after {id} must be a POST-only command or a child collection (R-4)",
        "C-2: /api/v1/project-baselines/{baselineId}/baseline-dependencies: a segment after {id} must be a POST-only command or a child collection (R-4)",
        "C-2: /api/v1/project-baselines/{baselineId}/baseline-milestones: a segment after {id} must be a POST-only command or a child collection (R-4)",
        "C-2: /api/v1/milestone-achievements/{achievementId}/evidence: a segment after {id} must be a POST-only command or a child collection (R-4)",
        "C-9: GET /api/v1/milestone-achievements/{achievementId}/evidence: collection GET without page/pageSize or cursor/pageSize (R-28)",
        "C-9: GET /api/v1/milestone-achievements/{achievementId}/evidence: 200 schema is not a page envelope (items + totalCount|nextCursor) (R-29/R-30)",
        "C-2: /api/v1/milestone-achievements/{achievementId}/evidence/{evidenceReferenceId}/withdraw nests deeper than collection/{id}/child/{id} (R-3)",
        "C-2: /api/v1/financial-commitments/{commitmentId}/documents: a segment after {id} must be a POST-only command or a child collection (R-4)",
        "C-9: GET /api/v1/financial-commitments/{commitmentId}/documents: collection GET without page/pageSize or cursor/pageSize (R-28)",
        "C-9: GET /api/v1/financial-commitments/{commitmentId}/documents: 200 schema is not a page envelope (items + totalCount|nextCursor) (R-29/R-30)",
        "C-2: /api/v1/financial-commitments/{commitmentId}/documents/{evidenceReferenceId}/withdraw nests deeper than collection/{id}/child/{id} (R-3)",
        "C-9: GET /api/v1/financial-portfolio-aggregates: collection GET without page/pageSize or cursor/pageSize (R-28)",
        "C-9: GET /api/v1/financial-portfolio-aggregates: 200 schema is not a page envelope (items + totalCount|nextCursor) (R-29/R-30)",
        "C-9: GET /api/v1/kpi-portfolio-aggregates: collection GET without page/pageSize or cursor/pageSize (R-28)",
        "C-9: GET /api/v1/kpi-portfolio-aggregates: 200 schema is not a page envelope (items + totalCount|nextCursor) (R-29/R-30)",
    ];

    /// <summary>A removed operation, status code, response property or enum value, or a changed type, format or nullability, fails (R-10).</summary>
    [Theory]
    [MemberData(nameof(Tags))]
    public async Task EachExecutionApiKeepsEveryPromiseOfTheSnapshot(string tag)
    {
        IReadOnlyList<string> breaks = OpenApiBreakingChanges.Find(OpenApiDocument.Snapshot(), await BuiltAsync(), tag);

        Assert.True(breaks.Count == 0, $"""
            The {tag} API breaks its contract (api-conventions R-10) against {OpenApiDocument.SnapshotPath}:
              {string.Join("\n  ", breaks)}
            A break ships only under R-11: rewrite the snapshot ({OpenApiDocument.UpdateVariable}=1) and call the break out in the PR.
            """);
    }

    /// <summary>The snapshot is what is guarded, so it must be the API as built: an addition not recorded could later be removed unnoticed.</summary>
    [Theory]
    [MemberData(nameof(Modules))]
    public async Task EachExecutionSnapshotIsTheApiAsBuilt(string tag, int operations)
    {
        JsonObject built = (await BuiltAsync()).Surface(tag);

        Assert.True(JsonNode.DeepEquals(OpenApiDocument.Snapshot().Surface(tag), built), $"""
            {OpenApiDocument.SnapshotPath} does not describe the {tag} API as built. Run the Execution contract tests with
            {OpenApiDocument.UpdateVariable}=1 and commit the rewritten file with the change.
            """);
        Assert.Equal(operations, built["paths"]!.AsObject().Count);
    }

    /// <summary>TASK-009's lint over the generated document (§9 row 5 (a)): on each surface, nothing beyond the platform's findings and those recorded.</summary>
    [Theory]
    [MemberData(nameof(Tags))]
    public async Task EachExecutionApiFollowsTheConventions(string tag)
    {
        IReadOnlyList<string> findings = await (await BuiltAsync()).LintAsync(tag);

        string[] unrecorded = [.. findings.Where(line => !OpenApiDocument.OpenPlatformFindings.Any(known => known.IsMatch(line)) && !RecordedFindings.Contains(line))];
        Assert.True(unrecorded.Length == 0, $"The {tag} API breaks the TASK-009 conventions:\n  {string.Join("\n  ", unrecorded)}");
    }

    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return await OpenApiDocument.BuiltAsync(client);
    }
}
