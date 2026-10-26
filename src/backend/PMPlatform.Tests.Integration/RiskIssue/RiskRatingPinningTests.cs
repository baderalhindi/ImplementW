using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;
using PMPlatform.Tests.Integration.Project;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.RiskIssue;

/// <summary>
/// Invariant 1, WF-06's side (risk-management.md D-4, D-13): a risk assessment's rating is pinned to the RISK_MATRIX version in force
/// when the assessment is made. Every read that serves a recorded rating serves that version's, whole, whatever is published later;
/// the request cannot choose the rating, the overall impact or the version; and the next assessment pins the version in force then.
/// </summary>
[Collection(RiskSuite.Name)]
public sealed class RiskRatingPinningTests(RiskTestHost host)
{
    private const string RatingSchema = "RiskRatingDetail";

    /// <summary>
    /// A risk identified ten days ago is assessed at 3 × 4 under version A, by a request that also claims a rating, an overall impact
    /// and a version of its own. Version B then rates that cell the other way and relabels every rating. Each read of the recorded
    /// assessment — the risk, the register, the history and the assessment itself — is unchanged in every field, as is a command's
    /// answer and the row, whose rating belongs to A. A reassessment of the same figures is rated by B and pins B.
    /// </summary>
    [Fact]
    public async Task EveryReadOfARecordedAssessmentServesTheRatingOfTheVersionItPinned()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await RiskDriver.SignInAsync(client);
        Crew crew = await client.SignInCrewAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid projectId = await host.ProjectAsync();
        Guid riskId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(
            sessions.EntityManager, RiskDriver.Risks, RiskDriver.RiskBody(projectId, identified: RiskDriver.Today.AddDays(-10))));
        object figures = RiskDriver.Assessment(dimensions, probability: 3, impact: 2, peak: 4);

        ResolvedConfiguration a = await MatrixInForceAsync();
        string ratedByA = a.RequireRiskRating(3, 4).Code;
        string ratedByB = ratedByA == "HIGH" ? "LOW" : "HIGH";
        JsonObject assessed = await RiskDriver.CommandOrFailAsync(client, sessions.Officer, riskId, "assess", RiskIssueApi.Claiming(figures, Guid.NewGuid(), "CRITICAL", ratedByB));
        JsonObject recorded = assessed["currentAssessment"]!.AsObject();
        Assert.Equal((a.VersionId.ToString(), ratedByA, 4), (recorded.Text("matrixConfigurationVersionId"), recorded["rating"]!.Text("code"), recorded["overallImpactLevel"]!.GetValue<int>()));
        string assessmentId = recorded.Text("id");

        Dictionary<string, JsonNode> before = await ReadsAsync(client, sessions, projectId, riskId, assessmentId);
        Assert.True(JsonNode.DeepEquals(recorded, before[$"GET {RiskDriver.Risks}/{{riskId}}"]), "The assess command and the risk serve different assessments.");
        string row = Assert.Single(await RowsAsync(riskId));
        Assert.Equal($"1|{a.VersionId}|{a.VersionId}|{ratedByA}|4", row);

        Guid b = await host.PublishMatrixAsync(client, crew, highFrom: ratedByA == "HIGH" ? 13 : 12, englishLabels: ("Tolerable", "Intolerable"));
        try
        {
            Assert.Equal((b, ratedByB), ((await MatrixInForceAsync()).VersionId, (await MatrixInForceAsync()).RequireRiskRating(3, 4).Code));

            Dictionary<string, JsonNode> after = await ReadsAsync(client, sessions, projectId, riskId, assessmentId);
            foreach ((string read, JsonNode served) in before)
            {
                Assert.True(JsonNode.DeepEquals(served, after[read]), $"{read} no longer serves the assessment as recorded:\n{served}\n{after[read]}");
            }

            JsonObject monitored = await RiskDriver.CommandOrFailAsync(client, sessions.EntityManager, riskId, "monitor");
            Assert.True(JsonNode.DeepEquals(recorded, monitored["currentAssessment"]), $"A command answers with another rating:\n{recorded}\n{monitored["currentAssessment"]}");
            Assert.Equal([row], await RowsAsync(riskId));

            JsonObject reassessed = (await RiskDriver.CommandOrFailAsync(client, sessions.Officer, riskId, "assess", RiskIssueApi.Claiming(figures, Guid.NewGuid(), "CRITICAL", ratedByA)))["currentAssessment"]!.AsObject();
            Assert.Equal((2, b.ToString(), ratedByB), (reassessed["versionNo"]!.GetValue<int>(), reassessed.Text("matrixConfigurationVersionId"), reassessed["rating"]!.Text("code")));
            Assert.Equal(ratedByB == "HIGH" ? "Intolerable" : "Tolerable", reassessed["rating"]!["label"]!.Text("en"));
            Assert.Equal([row, $"2|{b}|{b}|{ratedByB}|4"], await RowsAsync(riskId));
            JsonArray history = (await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Assessments}?riskId={riskId}"))["items"]!.AsArray();
            Assert.True(JsonNode.DeepEquals(before[$"GET {RiskDriver.Assessments}"], history.Single(v => v!.Text("id") == assessmentId)), "The first version changed when the second was recorded.");
        }
        finally
        {
            // The collection's other tests expect the host's labels in force.
            await host.PublishMatrixAsync(client, crew, highFrom: ratedByA == "HIGH" ? 12 : 13);
        }

        // Every read that can serve a rating was read: a new one fails here until it is added to ReadsAsync.
        Assert.Equal((await OpenApiDocument.FetchAsync(client)).ReadsServing(RiskIssueApi.RiskTag, RatingSchema), before.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>The recorded assessment as each read that serves a rating serves it, by <c>GET path</c>.</summary>
    private static async Task<Dictionary<string, JsonNode>> ReadsAsync(HttpClient client, RiskSessions sessions, Guid projectId, Guid riskId, string assessmentId)
    {
        static JsonNode Of(JsonObject page, string id) => page["items"]!.AsArray().Single(i => i!.Text("id") == id)!;

        return new Dictionary<string, JsonNode>
        {
            [$"GET {RiskDriver.Risks}/{{riskId}}"] = (await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}/{riskId}"))["currentAssessment"]!,
            [$"GET {RiskDriver.Risks}"] = Of(await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}?projectId={projectId}"), riskId.ToString())["currentAssessment"]!,
            [$"GET {RiskDriver.Assessments}"] = Of(await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Assessments}?riskId={riskId}"), assessmentId),
            [$"GET {RiskDriver.Assessments}/{{assessmentId}}"] = await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Assessments}/{assessmentId}"),
        };
    }

    /// <summary>Each recorded version as <c>no|pinned version|version of its rating row|rating code|overall impact</c>.</summary>
    private Task<IReadOnlyList<string>> RowsAsync(Guid riskId) => host.Database.QueryAsync($"""
        SELECT v.version_no || '|' || v.matrix_configuration_version_id || '|' || d.configuration_version_id || '|' || d.code || '|' || v.overall_impact_level
        FROM risk.risk_assessment_version v JOIN master_data_config.risk_rating_definition d ON d.id = v.risk_rating_definition_id
        WHERE v.risk_id = '{riskId}' ORDER BY v.version_no
        """);

    private async Task<ResolvedConfiguration> MatrixInForceAsync()
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IConfigurationResolver>().ResolveAsync("RISK_MATRIX", host.Clock.GetUtcNow(), CancellationToken.None);
    }
}
