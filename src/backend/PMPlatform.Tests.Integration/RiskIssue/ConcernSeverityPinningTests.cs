using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.ManagementConcern;
using PMPlatform.Tests.Integration.MasterDataConfig;
using PMPlatform.Tests.Integration.Project;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.RiskIssue;

/// <summary>
/// Invariant 1, WF-07's side (management-concern.md D-4, D-10): a concern's severity is computed by the RISK_MATRIX version in force
/// when it is assessed and pinned to it — a concern raised with impacts, and an issue a risk materialised into alike. Every read that
/// serves a severity serves the pinned one, whole, whatever is published later; the next assessment pins the version in force then.
/// </summary>
[Collection(ConcernSuite.Name)]
public sealed class ConcernSeverityPinningTests(ConcernTestHost host)
{
    private const string ConcernSchema = "ConcernDetail";

    /// <summary>
    /// A challenge raised at overall level 4 and an issue materialised from a risk peaking at 4 are both MAJOR under version A. Version
    /// B maps level 4 to CRITICAL. Each read of both — the concern and the register — is unchanged in every field, and so are their
    /// rows. A reassessment of the challenge's same impacts is CRITICAL and pins B; the issue, not reassessed, keeps A's MAJOR.
    /// </summary>
    [Fact]
    public async Task EveryReadOfAConcernServesTheSeverityOfTheVersionItPinned()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await ConcernDriver.SignInAsync(client);
        Crew crew = await client.SignInCrewAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid projectId = await host.ProjectAsync();
        string a = await host.Database.MatrixInForceAsync();

        Guid challengeId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(
            sessions.InternalManager, ConcernDriver.Concerns, ConcernDriver.ConcernBody(projectId, ConcernDriver.Impacts(dimensions, 4, 1), type: "CHALLENGE")));
        Guid riskId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(
            sessions.InternalManager, RiskDriver.Risks, RiskDriver.RiskBody(projectId, category: ConcernTestHost.RiskCategoryId)));
        await client.OkOrFailAsync(sessions.Officer, $"{RiskDriver.Risks}/{riskId}/assess", RiskDriver.Assessment(dimensions, probability: 2, impact: 1, peak: 4));
        JsonObject materialised = await client.OkOrFailAsync(sessions.InternalManager, $"{RiskDriver.Risks}/{riskId}/materialise",
            new { categoryItemId = ConcernTestHost.CategoryId, priorityItemId = ConcernTestHost.HighPriorityId });
        Guid issueId = Guid.Parse(Assert.Single(materialised["materialisedIssueIds"]!.AsArray())!.GetValue<string>());

        Dictionary<string, JsonNode> before = await ReadsAsync(client, sessions, projectId, challengeId, issueId);
        foreach (JsonNode concern in before.Values)
        {
            Assert.Equal((ConcernTestHost.MajorId.ToString(), 4, a), Severity(concern));
        }

        string[] rows = [.. await RowsAsync(challengeId, issueId)];
        Assert.Equal([$"{challengeId}|{ConcernTestHost.MajorId}|4|{a}", $"{issueId}|{ConcernTestHost.MajorId}|4|{a}"], rows);

        Guid b = await host.PublishScaleAsync(client, crew, new Dictionary<short, string>(ConcernDriver.DefaultMapping) { [4] = "TEST_CRITICAL" });
        try
        {
            Dictionary<string, JsonNode> after = await ReadsAsync(client, sessions, projectId, challengeId, issueId);
            foreach ((string read, JsonNode served) in before)
            {
                Assert.True(JsonNode.DeepEquals(served, after[read]), $"{read} no longer serves the severity as recorded:\n{served}\n{after[read]}");
            }

            Assert.Equal(rows, await RowsAsync(challengeId, issueId));

            JsonObject reassessed = await ConcernDriver.CommandOrFailAsync(client, sessions.InternalManager, challengeId, "assess", new { impacts = ConcernDriver.Impacts(dimensions, 4, 1) });
            Assert.Equal((ConcernTestHost.CriticalId.ToString(), 4, b.ToString()), Severity(reassessed));
            Assert.Equal([$"{challengeId}|{ConcernTestHost.CriticalId}|4|{b}", rows[1]], await RowsAsync(challengeId, issueId));
            Assert.Equal((ConcernTestHost.MajorId.ToString(), 4, a), Severity(await ConcernDriver.ConcernAsync(client, sessions.Officer, issueId)));
        }
        finally
        {
            // The collection's other tests expect the host's mapping in force.
            await host.PublishScaleAsync(client, crew, ConcernDriver.DefaultMapping);
        }

        // Every read that can serve a severity was read: a new one fails here until it is added to ReadsAsync.
        Assert.Equal(
            (await OpenApiDocument.FetchAsync(client)).ReadsServing(RiskIssueApi.ConcernTag, ConcernSchema),
            before.Keys.Select(k => k[..k.LastIndexOf(' ')]).Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>Both concerns as each read that serves a severity serves them, by <c>GET path</c> and concern.</summary>
    private static async Task<Dictionary<string, JsonNode>> ReadsAsync(HttpClient client, ConcernSessions sessions, Guid projectId, params Guid[] concernIds)
    {
        JsonArray register = (await client.GetOrFailAsync(sessions.Officer, $"{ConcernDriver.Concerns}?projectId={projectId}"))["items"]!.AsArray();
        Dictionary<string, JsonNode> reads = [];
        foreach (Guid id in concernIds)
        {
            reads[$"GET {ConcernDriver.Concerns}/{{concernId}} {id}"] = await ConcernDriver.ConcernAsync(client, sessions.Officer, id);
            reads[$"GET {ConcernDriver.Concerns} {id}"] = register.Single(c => c!.Text("id") == id.ToString())!;
        }

        return reads;
    }

    private static (string? SeverityItemId, int? OverallImpactLevel, string? VersionId) Severity(JsonNode concern) =>
        (concern["severityItemId"]?.GetValue<string>(), concern["overallImpactLevel"]?.GetValue<int>(), concern["severityConfigurationVersionId"]?.GetValue<string>());

    /// <summary>Each concern's stored severity as <c>id|severity item|overall level|pinned version</c>, in the order given.</summary>
    private async Task<IReadOnlyList<string>> RowsAsync(params Guid[] concernIds)
    {
        List<string> rows = [];
        foreach (Guid id in concernIds)
        {
            rows.Add(Assert.Single(await host.Database.QueryAsync(
                $"SELECT id || '|' || severity_item_id || '|' || overall_impact_level || '|' || severity_configuration_version_id FROM management_concern.management_concern WHERE id = '{id}'")));
        }

        return rows;
    }
}
