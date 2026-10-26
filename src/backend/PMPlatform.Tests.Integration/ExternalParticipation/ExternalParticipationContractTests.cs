using System.Text.Json.Nodes;
using OpenApiBreakingChanges = PMPlatform.Tests.Integration.Project.OpenApiBreakingChanges;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// The contract gates (api-conventions §9 row 5) for WF-13's API, as the other domains have them, and the workbook's validation check in the
/// contract itself: a submitted contribution has no edit operation — the only operations that act on one are the review's commands.
/// </summary>
/// <remarks>A change to this API is made with the snapshot: run these tests with <c>UPDATE_OPENAPI_SNAPSHOT=1</c> and commit the rewritten <c>docs/api/openapi.v1.json</c>.</remarks>
[Collection(ExternalParticipationSuite.Name)]
public sealed class ExternalParticipationContractTests(ExternalParticipationTestHost host)
{
    private const string Tag = "ExternalParticipation";
    private const string Contributions = "/api/v1/external-contributions";

    [Fact]
    public async Task TheApiKeepsEveryPromiseOfTheSnapshot()
    {
        IReadOnlyList<string> breaks = OpenApiBreakingChanges.Find(OpenApiDocument.Snapshot(), await BuiltAsync(), Tag);
        Assert.True(breaks.Count == 0, $"The {Tag} API breaks its contract (R-10):\n  {string.Join("\n  ", breaks)}");
    }

    /// <summary>The snapshot is the API as built, with its 22 operations.</summary>
    [Fact]
    public async Task TheSnapshotIsTheApiAsBuilt()
    {
        JsonObject built = (await BuiltAsync()).Surface(Tag);

        Assert.True(JsonNode.DeepEquals(OpenApiDocument.Snapshot().Surface(Tag), built),
            $"{OpenApiDocument.SnapshotPath} does not describe the {Tag} API as built. Run with {OpenApiDocument.UpdateVariable}=1 and commit the file.");
        Assert.Equal(22, built["paths"]!.AsObject().Count);
    }

    [Fact]
    public async Task TheApiFollowsTheConventions()
    {
        IReadOnlyList<string> findings = await (await BuiltAsync()).LintAsync(Tag);
        string[] unrecorded = [.. findings.Where(line => !OpenApiDocument.OpenPlatformFindings.Any(known => known.IsMatch(line)))];
        Assert.True(unrecorded.Length == 0, $"The {Tag} API breaks the TASK-009 conventions:\n  {string.Join("\n  ", unrecorded)}");
    }

    /// <summary>
    /// TASK-066's validation check, in the contract: a revision is read, created, and replaced only as a DRAFT (PUT, the responder's, refused
    /// once submitted); every other operation on one is a review or submission command — submit, start-review, accept, return, reject — and
    /// none of them takes a field value. No PATCH exists.
    /// </summary>
    [Fact]
    public async Task ASubmittedContributionHasNoEditOperationOnlyTheReviewCommands()
    {
        OpenApiDocument document = await BuiltAsync();
        const string Item = $"{Contributions}/{{externalContributionId}}";

        Assert.Equal(
            new[]
            {
                $"GET {Contributions}", $"POST {Contributions}", $"GET {Item}", $"PUT {Item}", $"POST {Item}/accept", $"POST {Item}/reject",
                $"POST {Item}/return", $"POST {Item}/start-review", $"POST {Item}/submit",
            }.Order(StringComparer.Ordinal),
            document.Operations(Tag).Where(o => o.Path.StartsWith(Contributions, StringComparison.Ordinal)).Select(o => o.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(document.Operations(Tag), o => o.Name.StartsWith("PATCH ", StringComparison.Ordinal));

        foreach (string decision in new[] { "accept", "return", "reject" })
        {
            JsonNode body = document.Operation($"{Item}/{decision}", "post")!["requestBody"]!;
            foreach (string schema in document.SchemasReachedBy(body))
            {
                Assert.DoesNotContain("fields", document.Surface(Tag)["schemas"]![schema]!["properties"]!.AsObject().Select(p => p.Key));
            }
        }
    }

    private async Task<OpenApiDocument> BuiltAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return await OpenApiDocument.BuiltAsync(client);
    }
}
