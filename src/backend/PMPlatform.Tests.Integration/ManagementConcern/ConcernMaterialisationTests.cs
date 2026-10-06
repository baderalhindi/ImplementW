using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.ManagementConcern;

/// <summary>
/// Edge 15 against WF-07's real register (risk-management.md F-1, closed here): a risk materialised into an issue, in the risk's
/// transaction, and the linkage read from both sides by the one foreign key, <c>originating_risk_id</c>.
/// </summary>
[Collection(ConcernSuite.Name)]
public sealed class ConcernMaterialisationTests(ConcernTestHost host)
{
    /// <summary>
    /// TASK-055's validation check, now end to end: the risk names the issue, the issue names the risk. The issue is WF-07's own — an
    /// ISSUE, OPEN, raised by whoever materialised it — and its severity is computed by WF-07's rule from the risk's impacts (overall 4:
    /// MAJOR), not copied from the risk's rating (BR-ISS-028).
    /// </summary>
    [Fact]
    public async Task AMaterialisedRiskAndItsIssueEachNameTheOther()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid riskId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(
            sessions.InternalManager, RiskDriver.Risks, RiskDriver.RiskBody(projectId, category: ConcernTestHost.RiskCategoryId)));
        JsonObject assessed = await client.OkOrFailAsync(sessions.Officer, $"{RiskDriver.Risks}/{riskId}/assess", RiskDriver.Assessment(await host.DimensionsAsync(), 3, 2, peak: 4));
        Assert.Equal("HIGH", assessed["currentAssessment"]!["rating"]!.Text("code"));

        JsonObject materialised = await client.OkOrFailAsync(sessions.InternalManager, $"{RiskDriver.Risks}/{riskId}/materialise", Issue(ConcernTestHost.CategoryId));
        string issueId = Assert.Single(materialised["materialisedIssueIds"]!.AsArray())!.GetValue<string>();

        JsonObject issue = await client.ConcernAsync(sessions.InternalManager, Guid.Parse(issueId));
        Assert.Equal(
            (riskId.ToString(), "ISSUE", "OPEN", ConcernDriver.Person(5).ToString(), ConcernTestHost.MajorId.ToString(), 4),
            (issue.Text("originatingRiskId"), issue.Text("concernType"), issue.Text("status"), issue.Text("raisedByUserId"), issue.Text("severityItemId"),
             issue["overallImpactLevel"]!.GetValue<int>()));
        Assert.Equal(4, issue["impacts"]!.AsArray().Count);
        Assert.Equal([$"{riskId}"], await host.Database.QueryAsync($"SELECT originating_risk_id::text FROM management_concern.management_concern WHERE id = '{issueId}'"));
        Assert.Equal([issueId], (await client.GetOrFailAsync(sessions.InternalManager, $"{RiskDriver.Risks}/{riskId}"))["materialisedIssueIds"]!.AsArray().Select(i => i!.GetValue<string>()));
        Assert.Contains(
            issueId,
            (await client.GetOrFailAsync(sessions.InternalManager, $"{ConcernDriver.Concerns}?projectId={projectId}&concernType=ISSUE"))["items"]!.AsArray().Select(c => c!.Text("id")));
        Assert.Equal([$"{riskId}"], await host.Database.QueryAsync($"""
            SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
            WHERE e.event_type = 'ManagementConcern.ConcernRaised' AND e.subject_id = '{issueId}' AND a.attribute_name = 'originating_risk_id'
            """));
    }

    /// <summary>One transaction (M-11): WF-07 refuses an issue of a draft category with its own code, and neither the issue nor the risk's materialisation is written.</summary>
    [Fact]
    public async Task AnIssueWf07RefusesLeavesTheRiskUnmaterialised()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid riskId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(
            sessions.InternalManager, RiskDriver.Risks, RiskDriver.RiskBody(await host.ProjectAsync(), category: ConcernTestHost.RiskCategoryId)));

        using (HttpResponseMessage refused = await client.PostAsync($"{RiskDriver.Risks}/{riskId}/materialise", sessions.InternalManager, Issue(ConcernTestHost.DraftCategoryId)))
        {
            await refused.AssertStatusAsync(HttpStatusCode.UnprocessableEntity, "CONCERN_CATEGORY_INVALID");
        }

        Assert.Equal(["|0"], await host.Database.QueryAsync($"""
            SELECT coalesce(r.materialised_at::text, '') || '|' || (SELECT count(*) FROM management_concern.management_concern c WHERE c.originating_risk_id = r.id)
            FROM risk.risk r WHERE r.id = '{riskId}'
            """));

        // A risk never assessed carries no impacts: its issue is raised unassessed, and WF-07 assesses it.
        JsonObject materialised = await client.OkOrFailAsync(sessions.InternalManager, $"{RiskDriver.Risks}/{riskId}/materialise", Issue(ConcernTestHost.CategoryId));
        JsonObject issue = await client.ConcernAsync(sessions.InternalManager, Guid.Parse(materialised["materialisedIssueIds"]![0]!.GetValue<string>()));
        Assert.Null(issue["severityItemId"]);
    }

    private static object Issue(Guid categoryItemId) => new { categoryItemId, priorityItemId = ConcernTestHost.HighPriorityId };
}
