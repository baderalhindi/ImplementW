using System.Text.Json.Nodes;
using OpenApiBreakingChanges = PMPlatform.Tests.Integration.Project.OpenApiBreakingChanges;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.Reports;

/// <summary>
/// The contract gates (api-conventions §9 row 5) for FG-02's API: R-7's asynchronous export (202, the job a resource with the ERD's lifecycle), R-8's
/// binary download, and the R-20(c) projection metadata on every section of a result.
/// </summary>
/// <remarks>A change to this API is made with the snapshot: run these tests with <c>UPDATE_OPENAPI_SNAPSHOT=1</c> and commit the rewritten <c>docs/api/openapi.v1.json</c>.</remarks>
[Collection(ReportSuite.Name)]
public sealed class ReportContractTests(ReportTestHost host)
{
    private const string Tag = "Reports";

    [Fact]
    public async Task TheApiKeepsEveryPromiseOfTheSnapshot()
    {
        IReadOnlyList<string> breaks = OpenApiBreakingChanges.Find(OpenApiDocument.Snapshot(), await BuiltAsync(), Tag);
        Assert.True(breaks.Count == 0, $"The {Tag} API breaks its contract (R-10):\n  {string.Join("\n  ", breaks)}");
    }

    /// <summary>The snapshot is the API as built, with its 24 operations.</summary>
    [Fact]
    public async Task TheSnapshotIsTheApiAsBuilt()
    {
        JsonObject built = (await BuiltAsync()).Surface(Tag);

        Assert.True(JsonNode.DeepEquals(OpenApiDocument.Snapshot().Surface(Tag), built),
            $"{OpenApiDocument.SnapshotPath} does not describe the {Tag} API as built. Run with {OpenApiDocument.UpdateVariable}=1 and commit the file.");
        Assert.Equal(24, built["paths"]!.AsObject().Count);
    }

    [Fact]
    public async Task TheApiFollowsTheConventions()
    {
        IReadOnlyList<string> findings = await (await BuiltAsync()).LintAsync(Tag);
        string[] unrecorded = [.. findings.Where(line => !OpenApiDocument.OpenPlatformFindings.Any(known => known.IsMatch(line)))];
        Assert.True(unrecorded.Length == 0, $"The {Tag} API breaks the TASK-009 conventions:\n  {string.Join("\n  ", unrecorded)}");
    }

    /// <summary>
    /// R-7: an export is 202 and its job's status is exactly the ERD's lifecycle; R-8: the output is a binary GET; the formats are ADR-005's three;
    /// and every section and cell of a result carries R-20(c)'s metadata or its own freshness, with UNKNOWN reasons that are never 0.
    /// </summary>
    [Fact]
    public async Task ExportsAreJobsAndEveryResultCarriesTheProjectionMetadataContract()
    {
        JsonObject document = (await BuiltAsync()).Surface(Tag);
        JsonObject paths = document["paths"]!.AsObject();
        JsonObject schemas = document["schemas"]!.AsObject();

        foreach (string export in new[] { "POST /api/v1/reports/{reportCode}/export", "POST /api/v1/report-explorer/export" })
        {
            Assert.Equal(["202"], paths[export]!["responses"]!.AsObject().Select(r => r.Key).Where(k => k.StartsWith('2')));
        }

        Assert.Contains("application/octet-stream", paths["GET /api/v1/report-jobs/{jobId}/content"]!["responses"]!["200"]!["content"]!.AsObject().Select(c => c.Key));
        Assert.Equal(["CANCELLED", "COMPLETED", "EXPIRED", "FAILED", "QUEUED", "REQUESTED", "RUNNING", "VALIDATING"], Values(schemas, "ReportJobStatus"));
        Assert.Equal(["CSV", "PDF", "XLSX"], Values(schemas, "ReportExportFormat"));
        Assert.Equal(["MISSING", "NOT_APPLICABLE", "RESTRICTED", "SOURCE_UNAVAILABLE"], Values(schemas, "ReportUnknownReason"));
        Assert.Contains("projection", schemas["ReportSection"]!["properties"]!.AsObject().Select(p => p.Key));
        Assert.Equal(["asOf", "freshness", "isMasked", "label", "unknownReason", "value"], schemas["ReportCell"]!["properties"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal));
    }

    /// <summary>An enumeration's values; the generator lists null among those of an enumeration used only as nullable, which is not a value.</summary>
    private static IEnumerable<string> Values(JsonObject schemas, string name) =>
        schemas[name]!["enum"]!.AsArray().OfType<JsonNode>().Select(v => v.GetValue<string>()).Order(StringComparer.Ordinal);

    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return await OpenApiDocument.BuiltAsync(client);
    }
}
