using System.Net;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Risk;

/// <summary>
/// A risk materialised into an issue (edge 15), with WF-07 played by <see cref="TestIssueRegister"/> until TASK-057 builds it: the
/// issue holds the risk as its origin, the risk records when, both in one transaction, and the linkage reads from both sides.
/// </summary>
[Collection(RiskSuite.Name)]
public sealed class RiskMaterialisationTests(RiskTestHost host)
{
    /// <summary>
    /// TASK-055's validation check: materialise a risk into an issue and the linkage is queryable from both sides — the risk, read
    /// alone or in the register, names the issue, and the issue's <c>originating_risk_id</c> names the risk. The risk keeps its
    /// status, and is materialised once.
    /// </summary>
    [Fact]
    public async Task AMaterialisedRiskAndItsIssueEachNameTheOther()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, projectId);
        await client.CommandOrFailAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(await host.DimensionsAsync(), 4, 3));

        JsonObject materialised = await client.CommandOrFailAsync(sessions.EntityManager, riskId, "materialise", Issue(RiskTestHost.ConcernCategoryId));
        Assert.Equal("ASSESSED", materialised.Text("status"));
        Assert.NotNull(materialised["materialisedAt"]);

        // The risk side, alone and in the register.
        string issueId = Assert.Single(materialised["materialisedIssueIds"]!.AsArray())!.GetValue<string>();
        Assert.Equal([issueId], (await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}/{riskId}"))["materialisedIssueIds"]!.AsArray().Select(i => i!.GetValue<string>()));
        JsonObject listed = (await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}?projectId={projectId}"))["items"]![0]!.AsObject();
        Assert.Equal([issueId], listed["materialisedIssueIds"]!.AsArray().Select(i => i!.GetValue<string>()));

        // The issue side, by WF-07's one foreign key.
        Assert.Equal([$"{riskId} {projectId} OPEN"], await host.Database.QueryAsync($"SELECT originating_risk_id || ' ' || project_id || ' ' || status FROM test_issue.issue WHERE id = '{issueId}'"));

        using (HttpResponseMessage twice = await client.CommandAsync(sessions.EntityManager, riskId, "materialise", Issue(RiskTestHost.ConcernCategoryId)))
        {
            Assert.Equal((HttpStatusCode.Conflict, "RISK_ALREADY_MATERIALISED"), await twice.RefusalAsync());
        }

        Assert.Equal(
            [issueId],
            await host.Database.QueryAsync($"""
                SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.event_type = 'Risk.RiskMaterialised' AND e.subject_id = '{riskId}' AND a.attribute_name = 'management_concern_id'
                """));
    }

    /// <summary>One transaction (ADR-003 M-11): when WF-07 refuses the issue, the risk is not marked materialised, and WF-07's code is the answer.</summary>
    [Fact]
    public async Task ARefusedIssueLeavesTheRiskUnmaterialised()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());

        using (HttpResponseMessage refused = await client.CommandAsync(sessions.EntityManager, riskId, "materialise", Issue(RiskTestHost.RefusedConcernCategoryId)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, TestIssueRegister.RefusalCode), await refused.RefusalAsync());
        }

        Assert.Equal([""], await host.Database.QueryAsync($"SELECT coalesce(materialised_at::text, '') FROM risk.risk WHERE id = '{riskId}'"));
        Assert.Empty((await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}/{riskId}"))["materialisedIssueIds"]!.AsArray());
        Assert.Empty(await host.Database.QueryAsync($"SELECT id::text FROM test_issue.issue WHERE originating_risk_id = '{riskId}'"));

        // A closed risk is not materialised: it is reopened first.
        await client.CommandOrFailAsync(sessions.EntityManager, riskId, "close", RiskDriver.Rationale("No longer applies."));
        using HttpResponseMessage closed = await client.CommandAsync(sessions.EntityManager, riskId, "materialise", Issue(RiskTestHost.ConcernCategoryId));
        Assert.Equal((HttpStatusCode.Conflict, "RISK_CLOSED"), await closed.RefusalAsync());
    }

    private static object Issue(Guid categoryItemId) => new { categoryItemId, priorityItemId = RiskTestHost.PriorityId };
}
