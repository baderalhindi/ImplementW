using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.ManagementConcern;
using PMPlatform.Tests.Integration.Project;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.RiskIssue;

/// <summary>
/// Invariant 2 (management-concern.md D-4; risk-management.md §4): Severity is never accepted as raw client input — nor is a risk's
/// rating, the overall impact or the pinned version. Held twice: no request either API documents can carry one, and every write that
/// raises or changes a concern, sent one anyway, stores and serves the severity the scale in force computes.
/// </summary>
[Collection(ConcernSuite.Name)]
public sealed class SeverityInputTests(ConcernTestHost host)
{
    private const string MaterialiseTemplate = $"{RiskDriver.Risks}/{{riskId}}/materialise";

    /// <summary>
    /// The static half: no property of any WF-06 or WF-07 request body, at any depth, names a computed field. A severity added to a
    /// request model is documented by the generated OpenAPI document, so it fails here whatever the service then does with it.
    /// </summary>
    [Fact]
    public async Task NoWf06OrWf07RequestCanCarryAComputedField()
    {
        using HttpClient client = host.Api.CreateClient();
        OpenApiDocument document = await OpenApiDocument.FetchAsync(client);

        List<(string Name, IReadOnlySet<string> Properties)> requests =
        [
            .. from tag in new[] { RiskIssueApi.RiskTag, RiskIssueApi.ConcernTag }
               from operation in document.Operations(tag)
               select (operation.Name, document.RequestPropertyNames(operation.Operation)),
        ];
        string[] computed = [.. requests.SelectMany(r => r.Properties.Where(p => RiskIssueApi.ComputedField.IsMatch(p)).Select(p => $"{r.Name}: {p}"))];

        Assert.True(computed.Length == 0, $"A request accepts a field the server computes:\n  {string.Join("\n  ", computed)}");

        // The scan reads what clients send, so it cannot pass on nothing: every body has properties, the impacts' levels among them.
        Assert.All(document.Writes(RiskIssueApi.RiskTag).Concat(document.Writes(RiskIssueApi.ConcernTag)), w => Assert.NotEmpty(requests.Single(r => r.Name == w).Properties));
        Assert.Contains("impactLevel", requests.Single(r => r.Name == $"POST {ConcernDriver.Concerns}").Properties);
        Assert.Contains("impactLevel", requests.Single(r => r.Name == $"POST {RiskDriver.Risks}/{{riskId}}/assess").Properties);
    }

    /// <summary>
    /// The behavioural half. Every WF-07 write that takes a body, and WF-06's materialisation (edge 15, the other way a concern is
    /// raised), is sent with every computed field claiming CRITICAL at level 5 under a version that does not exist. The concern's
    /// severity, in the answer, on a read and in its row, is each time the one its impacts compute under the scale in force — MINOR
    /// at 2, MAJOR at 3, the issue MAJOR at 4 — and the escalation notifies that severity, not the claim.
    /// </summary>
    [Fact]
    public async Task EveryWriteThatRaisesOrChangesAConcernComputesItsSeverity()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await ConcernDriver.SignInAsync(client);
        Guid[] dimensions = await host.DimensionsAsync();
        Guid projectId = await host.ProjectAsync();
        string scale = await host.Database.MatrixInForceAsync();
        const string concern = $"{ConcernDriver.Concerns}/{{concernId}}";
        const string escalation = $"{ConcernDriver.Escalations}/{{escalationId}}";
        List<string> sent = [];

        JsonObject raised = await ClaimingAsync(HttpMethod.Post, ConcernDriver.Concerns, ConcernDriver.Concerns, sessions.InternalManager, HttpStatusCode.Created,
            ConcernDriver.ConcernBody(projectId, ConcernDriver.Impacts(dimensions, 1, 2, 1)));
        Guid concernId = AdministrationApi.IdOf(raised);
        string path = $"{ConcernDriver.Concerns}/{concernId}";
        await AssertComputedAsync(concernId, raised, ConcernTestHost.MinorId, 2);

        string etag;
        using (HttpResponseMessage read = await client.GetAsync(path, sessions.InternalManager))
        {
            etag = AdministrationApi.ETagOf(read);
        }

        object changes = new
        {
            title = new { text = "Site access blocked", language = "en" },
            description = new { text = "The access road is closed.", language = "en" },
            categoryItemId = ConcernTestHost.CategoryId,
            priorityItemId = ConcernTestHost.LowPriorityId,
        };
        await AssertComputedAsync(concernId, await ClaimingAsync(HttpMethod.Put, concern, path, sessions.InternalManager, HttpStatusCode.OK, changes, etag), ConcernTestHost.MinorId, 2);
        await AssertComputedAsync(concernId, await ClaimingAsync(HttpMethod.Post, $"{concern}/assess", $"{path}/assess", sessions.InternalManager, HttpStatusCode.OK,
            new { impacts = ConcernDriver.Impacts(dimensions, 3, 1) }), ConcernTestHost.MajorId, 3);
        await AssertComputedAsync(concernId, await ClaimingAsync(HttpMethod.Post, $"{concern}/assign", $"{path}/assign", sessions.InternalManager, HttpStatusCode.OK,
            new { assigneeUserId = ConcernDriver.Person(3) }), ConcernTestHost.MajorId, 3);
        await ConcernDriver.CommandOrFailAsync(client, sessions.InternalManager, concernId, "start");

        string escalationId = (await ClaimingAsync(HttpMethod.Post, ConcernDriver.Escalations, ConcernDriver.Escalations, sessions.InternalManager, HttpStatusCode.Created,
            new { managementConcernId = concernId, reason = new { text = "Blocked for two weeks.", language = "en" } })).Text("id");
        await AssertComputedAsync(concernId, null, ConcernTestHost.MajorId, 3);
        Assert.Equal(["TEST_MAJOR"], await host.Database.QueryAsync($"""
            SELECT p->>'value' FROM common.outbox_message m, jsonb_array_elements(m.payload->'data'->'parameters') p
            WHERE m.message_key = '{ConcernDriver.EscalatedEvent}:{escalationId}' AND p->>'name' = 'severity'
            """));
        await ClaimingAsync(HttpMethod.Post, $"{escalation}/resolve", $"{ConcernDriver.Escalations}/{escalationId}/resolve", sessions.DepartmentManager, HttpStatusCode.OK,
            ConcernDriver.Resolution("Utility owner reopens the road on Sunday."));
        await AssertComputedAsync(concernId, null, ConcernTestHost.MajorId, 3);

        JsonObject submitted = await ClaimingAsync(HttpMethod.Post, $"{concern}/submit-resolution", $"{path}/submit-resolution", sessions.InternalManager, HttpStatusCode.OK,
            ConcernDriver.Resolution("Temporary access agreed with the utility owner."));
        Assert.Equal("PENDING_VALIDATION", submitted.Text("status"));
        await AssertComputedAsync(concernId, submitted, ConcernTestHost.MajorId, 3);

        // Edge 15: an issue raised from a risk peaking at 4 is MAJOR at 4, computed by WF-07, whatever the materialisation claims.
        Guid riskId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(
            sessions.InternalManager, RiskDriver.Risks, RiskDriver.RiskBody(projectId, category: ConcernTestHost.RiskCategoryId)));
        await client.OkOrFailAsync(sessions.Officer, $"{RiskDriver.Risks}/{riskId}/assess", RiskDriver.Assessment(dimensions, probability: 2, impact: 1, peak: 4));
        JsonObject materialised = await ClaimingAsync(HttpMethod.Post, MaterialiseTemplate, $"{RiskDriver.Risks}/{riskId}/materialise", sessions.InternalManager,
            HttpStatusCode.OK, new { categoryItemId = ConcernTestHost.CategoryId, priorityItemId = ConcernTestHost.HighPriorityId });
        await AssertComputedAsync(Guid.Parse(Assert.Single(materialised["materialisedIssueIds"]!.AsArray())!.GetValue<string>()), null, ConcernTestHost.MajorId, 4);

        // Every write that can raise or change a concern was sent a claim: a new one fails here until it is added above.
        OpenApiDocument document = await OpenApiDocument.FetchAsync(client);
        Assert.Equal(document.Writes(RiskIssueApi.ConcernTag).Append($"POST {MaterialiseTemplate}").Order(StringComparer.Ordinal), sent.Order(StringComparer.Ordinal));

        async Task<JsonObject> ClaimingAsync(HttpMethod method, string template, string target, string token, HttpStatusCode expected, object body, string? ifMatch = null)
        {
            sent.Add($"{method.Method} {template}");
            using HttpResponseMessage response = await client.SendAsync(method, target, token, RiskIssueApi.Claiming(body, ConcernTestHost.CriticalId, "TEST_CRITICAL", "HIGH"), ifMatch);
            Assert.True(response.StatusCode == expected, $"{method} {target}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            return await response.ReadObjectAsync();
        }

        // The concern's severity as the write answered it (when it answers with the concern), as a read serves it and as its row holds it.
        async Task AssertComputedAsync(Guid id, JsonObject? answered, Guid severityItemId, int level)
        {
            (string, int, string) computed = (severityItemId.ToString(), level, scale);
            (string, int, string) read = Severity(await ConcernDriver.ConcernAsync(client, sessions.Officer, id));
            string stored = Assert.Single(await host.Database.QueryAsync(
                $"SELECT severity_item_id || '|' || overall_impact_level || '|' || severity_configuration_version_id FROM management_concern.management_concern WHERE id = '{id}'"));
            Assert.True(
                (answered is null || Severity(answered) == computed) && read == computed && stored == $"{severityItemId}|{level}|{scale}",
                $"After {sent[^1]} claiming CRITICAL, the severity is not the computed {computed}: answered {(answered is null ? "-" : Severity(answered))}, read {read}, stored {stored}");
        }
    }

    private static (string, int, string) Severity(JsonObject concern) =>
        (concern["severityItemId"]?.GetValue<string>() ?? "none", concern["overallImpactLevel"]?.GetValue<int>() ?? 0, concern["severityConfigurationVersionId"]?.GetValue<string>() ?? "none");
}
