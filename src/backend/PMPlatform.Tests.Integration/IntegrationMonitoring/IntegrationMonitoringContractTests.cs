using System.Text.Json.Nodes;
using OpenApiBreakingChanges = PMPlatform.Tests.Integration.Project.OpenApiBreakingChanges;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// The contract gates (api-conventions §9 row 5) for FG-05's API (TASK-075): the snapshot is the API as built and the conventions hold. Every
/// write is a command on FG-05's own records — a retry, a reconciliation, an alert's handling, an integration's activation — never a write
/// to a source record.
/// </summary>
/// <remarks>A change to this API is made with the snapshot: run these tests with <c>UPDATE_OPENAPI_SNAPSHOT=1</c> and commit the rewritten <c>docs/api/openapi.v1.json</c>.</remarks>
[Collection(IntegrationMonitoringSuite.Name)]
public sealed class IntegrationMonitoringContractTests(IntegrationMonitoringTestHost host)
{
    private const string Tag = "IntegrationMonitoring";

    /// <summary>
    /// The checker reads every GET whose path does not end in an id as a collection. The catalogue is one representation — every
    /// integration with the environment it describes, a handful of rows with nothing to page — as AuditActivity's access view is.
    /// </summary>
    private static readonly string[] RecordedFindings =
    [
        "C-9: GET /api/v1/integrations: collection GET without page/pageSize or cursor/pageSize (R-28)",
        "C-9: GET /api/v1/integrations: 200 schema is not a page envelope (items + totalCount|nextCursor) (R-29/R-30)",
    ];

    [Fact]
    public async Task TheApiKeepsEveryPromiseOfTheSnapshot()
    {
        IReadOnlyList<string> breaks = OpenApiBreakingChanges.Find(OpenApiDocument.Snapshot(), await BuiltAsync(), Tag);
        Assert.True(breaks.Count == 0, $"The {Tag} API breaks its contract (R-10):\n  {string.Join("\n  ", breaks)}");
    }

    /// <summary>The snapshot is the API as built, with its 15 operations.</summary>
    [Fact]
    public async Task TheSnapshotIsTheApiAsBuilt()
    {
        JsonObject built = (await BuiltAsync()).Surface(Tag);

        Assert.True(JsonNode.DeepEquals(OpenApiDocument.Snapshot().Surface(Tag), built),
            $"{OpenApiDocument.SnapshotPath} does not describe the {Tag} API as built. Run with {OpenApiDocument.UpdateVariable}=1 and commit the file.");
        Assert.Equal(15, built["paths"]!.AsObject().Count);
    }

    [Fact]
    public async Task TheApiFollowsTheConventions()
    {
        IReadOnlyList<string> findings = await (await BuiltAsync()).LintAsync(Tag);
        string[] unrecorded = [.. findings.Where(line => !OpenApiDocument.OpenPlatformFindings.Any(known => known.IsMatch(line)) && !RecordedFindings.Contains(line, StringComparer.Ordinal))];
        Assert.True(unrecorded.Length == 0, $"The {Tag} API breaks the TASK-009 conventions:\n  {string.Join("\n  ", unrecorded)}");
    }

    [Fact]
    public async Task EveryWriteIsACommandOnFg05sOwnRecords()
    {
        JsonObject paths = (await BuiltAsync()).Surface(Tag)["paths"]!.AsObject();

        Assert.Equal(
            [
                "POST /api/v1/integration-invocations/{invocationId}/retry",
                "POST /api/v1/integration-reconciliations",
                "POST /api/v1/integrations/{integrationId}/disable",
                "POST /api/v1/integrations/{integrationId}/enable",
                "POST /api/v1/operational-alerts/{alertId}/acknowledge",
                "POST /api/v1/operational-alerts/{alertId}/dismiss",
                "POST /api/v1/operational-alerts/{alertId}/resolve",
            ],
            paths.Select(p => p.Key).Where(k => !k.StartsWith("GET ", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }

    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return await OpenApiDocument.BuiltAsync(client);
    }
}
