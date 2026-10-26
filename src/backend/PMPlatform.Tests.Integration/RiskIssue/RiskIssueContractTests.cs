using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Project;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.RiskIssue;

/// <summary>
/// TASK-059's contract gates (api-conventions §9 row 5 (a) and (b)) for the risk and issue domain: the WF-06 and WF-07 APIs against
/// the committed snapshot, as TASK-043 does for WF-01 and TASK-054 for the execution domain. The responses on the wire are checked
/// by <see cref="RiskResponseContractTests"/> and <see cref="ConcernResponseContractTests"/>.
/// </summary>
/// <remarks>
/// A change to one of these APIs is made with the snapshot: run these tests with <c>UPDATE_OPENAPI_SNAPSHOT=1</c>, commit the
/// rewritten <c>docs/api/openapi.v1.json</c>, and, if <see cref="EachRiskIssueApiKeepsEveryPromiseOfTheSnapshot"/> failed first,
/// call the break out in the PR as R-11 requires.
/// </remarks>
[Collection(RiskSuite.Name)]
public sealed class RiskIssueContractTests(RiskTestHost host)
{
    /// <summary>Each module's tag and the number of operations it carries, so an operation added or dropped is seen.</summary>
    public static TheoryData<string, int> Modules => new()
    {
        { RiskIssueApi.RiskTag, 22 },
        { RiskIssueApi.ConcernTag, 15 },
    };

    public static TheoryData<string> Tags => [.. Modules.Select(m => (string)m[0])];

    /// <summary>A removed operation, status code, response property or enum value, or a changed type, format or nullability, fails (R-10).</summary>
    [Theory]
    [MemberData(nameof(Tags))]
    public async Task EachRiskIssueApiKeepsEveryPromiseOfTheSnapshot(string tag)
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
    public async Task EachRiskIssueSnapshotIsTheApiAsBuilt(string tag, int operations)
    {
        JsonObject built = (await BuiltAsync()).Surface(tag);

        Assert.True(JsonNode.DeepEquals(OpenApiDocument.Snapshot().Surface(tag), built), $"""
            {OpenApiDocument.SnapshotPath} does not describe the {tag} API as built. Run the RiskIssue contract tests with
            {OpenApiDocument.UpdateVariable}=1 and commit the rewritten file with the change.
            """);
        Assert.Equal(operations, built["paths"]!.AsObject().Count);
    }

    /// <summary>
    /// TASK-009's lint over the generated document (§9 row 5 (a)): on each surface, nothing beyond the platform's findings
    /// (<see cref="OpenApiDocument.OpenPlatformFindings"/>). Neither module carries a finding of its own (risk-management.md §7 row 6,
    /// management-concern.md §7 row 6), so none is recorded here.
    /// </summary>
    [Theory]
    [MemberData(nameof(Tags))]
    public async Task EachRiskIssueApiFollowsTheConventions(string tag)
    {
        IReadOnlyList<string> findings = await (await BuiltAsync()).LintAsync(tag);

        string[] unrecorded = [.. findings.Where(line => !OpenApiDocument.OpenPlatformFindings.Any(known => known.IsMatch(line)))];
        Assert.True(unrecorded.Length == 0, $"The {tag} API breaks the TASK-009 conventions:\n  {string.Join("\n  ", unrecorded)}");
    }

    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return await OpenApiDocument.BuiltAsync(client);
    }
}
