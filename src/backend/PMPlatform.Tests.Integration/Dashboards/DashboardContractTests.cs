using System.Text.Json.Nodes;
using OpenApiBreakingChanges = PMPlatform.Tests.Integration.Project.OpenApiBreakingChanges;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.Dashboards;

/// <summary>
/// The contract gates (api-conventions §9 row 5) for FG-01's API, and the R-20(c) projection metadata contract in it: every widget result
/// carries <c>projection</c> with exactly the four fields the convention names.
/// </summary>
/// <remarks>A change to this API is made with the snapshot: run these tests with <c>UPDATE_OPENAPI_SNAPSHOT=1</c> and commit the rewritten <c>docs/api/openapi.v1.json</c>.</remarks>
[Collection(DashboardSuite.Name)]
public sealed class DashboardContractTests(DashboardTestHost host)
{
    private const string Tag = "Dashboards";

    [Fact]
    public async Task TheApiKeepsEveryPromiseOfTheSnapshot()
    {
        IReadOnlyList<string> breaks = OpenApiBreakingChanges.Find(OpenApiDocument.Snapshot(), await BuiltAsync(), Tag);
        Assert.True(breaks.Count == 0, $"The {Tag} API breaks its contract (R-10):\n  {string.Join("\n  ", breaks)}");
    }

    /// <summary>The snapshot is the API as built, with its 12 operations.</summary>
    [Fact]
    public async Task TheSnapshotIsTheApiAsBuilt()
    {
        JsonObject built = (await BuiltAsync()).Surface(Tag);

        Assert.True(JsonNode.DeepEquals(OpenApiDocument.Snapshot().Surface(Tag), built),
            $"{OpenApiDocument.SnapshotPath} does not describe the {Tag} API as built. Run with {OpenApiDocument.UpdateVariable}=1 and commit the file.");
        Assert.Equal(12, built["paths"]!.AsObject().Count);
    }

    [Fact]
    public async Task TheApiFollowsTheConventions()
    {
        IReadOnlyList<string> findings = await (await BuiltAsync()).LintAsync(Tag);
        string[] unrecorded = [.. findings.Where(line => !OpenApiDocument.OpenPlatformFindings.Any(known => known.IsMatch(line)))];
        Assert.True(unrecorded.Length == 0, $"The {Tag} API breaks the TASK-009 conventions:\n  {string.Join("\n  ", unrecorded)}");
    }

    /// <summary>R-20(c): <c>projection</c> is <c>{ semanticState, freshness, asOf, coverage }</c> and its enumerations are the convention's.</summary>
    [Fact]
    public async Task EveryWidgetResultCarriesTheProjectionMetadataContract()
    {
        JsonObject schemas = (await BuiltAsync()).Surface(Tag)["schemas"]!.AsObject();

        Assert.Contains("projection", schemas["DashboardWidgetResult"]!["properties"]!.AsObject().Select(p => p.Key));
        Assert.Equal(["asOf", "coverage", "freshness", "semanticState"], schemas["ProjectionMeta"]!["properties"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal(["CURRENT_LIVE", "HISTORICAL_SNAPSHOT", "PUBLISHED_OFFICIAL"], Values(schemas, "ProjectionSemanticState"));
        Assert.Equal(["FRESH", "STALE", "UNKNOWN"], Values(schemas, "ProjectionFreshness"));
        Assert.Equal(["COMPLETE", "NONE", "PARTIAL"], Values(schemas, "ProjectionCoverage"));
    }

    private static IEnumerable<string> Values(JsonObject schemas, string name) =>
        schemas[name]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()).Order(StringComparer.Ordinal);

    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return await OpenApiDocument.BuiltAsync(client);
    }
}
