using System.Text.Json.Nodes;
using OpenApiBreakingChanges = PMPlatform.Tests.Integration.Project.OpenApiBreakingChanges;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// The contract gates (api-conventions §9 row 5) for FG-06's API (TASK-073): the snapshot is the API as built, the conventions hold,
/// and no operation writes an audit event — the API has no route that could edit or delete one (AUD-CC-06).
/// </summary>
/// <remarks>A change to this API is made with the snapshot: run these tests with <c>UPDATE_OPENAPI_SNAPSHOT=1</c> and commit the rewritten <c>docs/api/openapi.v1.json</c>.</remarks>
[Collection(AuditActivitySuite.Name)]
public sealed class AuditActivityContractTests(AuditActivityTestHost host)
{
    private const string Tag = "AuditActivity";

    /// <summary>
    /// The checker reads every GET whose path does not end in an id as a collection. These two are single representations — what the
    /// caller may do (AUD-API-014) and the pipeline's health (AUD-API-039) — with nothing to page, as FinancialKpi's portfolio aggregates
    /// are (<c>ExecutionContractTests</c>).
    /// </summary>
    private static readonly string[] RecordedFindings =
    [
        "C-9: GET /api/v1/audit-access: collection GET without page/pageSize or cursor/pageSize (R-28)",
        "C-9: GET /api/v1/audit-access: 200 schema is not a page envelope (items + totalCount|nextCursor) (R-29/R-30)",
        "C-9: GET /api/v1/audit-ingestion-status: collection GET without page/pageSize or cursor/pageSize (R-28)",
        "C-9: GET /api/v1/audit-ingestion-status: 200 schema is not a page envelope (items + totalCount|nextCursor) (R-29/R-30)",
    ];

    [Fact]
    public async Task TheApiKeepsEveryPromiseOfTheSnapshot()
    {
        IReadOnlyList<string> breaks = OpenApiBreakingChanges.Find(OpenApiDocument.Snapshot(), await BuiltAsync(), Tag);
        Assert.True(breaks.Count == 0, $"The {Tag} API breaks its contract (R-10):\n  {string.Join("\n  ", breaks)}");
    }

    /// <summary>The snapshot is the API as built, with its 8 operations.</summary>
    [Fact]
    public async Task TheSnapshotIsTheApiAsBuilt()
    {
        JsonObject built = (await BuiltAsync()).Surface(Tag);

        Assert.True(JsonNode.DeepEquals(OpenApiDocument.Snapshot().Surface(Tag), built),
            $"{OpenApiDocument.SnapshotPath} does not describe the {Tag} API as built. Run with {OpenApiDocument.UpdateVariable}=1 and commit the file.");
        Assert.Equal(8, built["paths"]!.AsObject().Count);
    }

    [Fact]
    public async Task TheApiFollowsTheConventions()
    {
        IReadOnlyList<string> findings = await (await BuiltAsync()).LintAsync(Tag);
        string[] unrecorded = [.. findings.Where(line => !OpenApiDocument.OpenPlatformFindings.Any(known => known.IsMatch(line)) && !RecordedFindings.Contains(line, StringComparer.Ordinal))];
        Assert.True(unrecorded.Length == 0, $"The {Tag} API breaks the TASK-009 conventions:\n  {string.Join("\n  ", unrecorded)}");
    }

    /// <summary>Every audit-event operation reads; the only writes are the three operations on the store, none of which names an event.</summary>
    [Fact]
    public async Task NoOperationWritesAnAuditEvent()
    {
        JsonObject paths = (await BuiltAsync()).Surface(Tag)["paths"]!.AsObject();

        Assert.All(paths.Select(p => p.Key).Where(k => k.Contains("/audit-events", StringComparison.Ordinal)), operation => Assert.StartsWith("GET ", operation, StringComparison.Ordinal));
        Assert.Equal(
            ["POST /api/v1/activity-projection-checks", "POST /api/v1/activity-projection-rebuilds", "POST /api/v1/audit-integrity-verifications"],
            paths.Select(p => p.Key).Where(k => !k.StartsWith("GET ", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }

    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return await OpenApiDocument.BuiltAsync(client);
    }
}
