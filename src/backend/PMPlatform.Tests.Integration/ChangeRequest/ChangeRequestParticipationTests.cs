using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ChangeRequest;

/// <summary>
/// ADR-013's amendment to TASK-060: entity Project Managers may raise a change request; materiality, approval and implementation remain
/// AHDA's. With the shipped grants: R04 views, raises and implements at OWN, the application refusing implementation to an external
/// holder; R03 views and starts reviews at DEPT.
/// </summary>
[Collection(ChangeRequestSuite.Name)]
public sealed class ChangeRequestParticipationTests(ChangeRequestTestHost host)
{
    /// <summary>
    /// The entity Project Manager (local.r08, external) raises, edits, previews, submits and follows a change request on the project
    /// they manage, and sets no classification; the review and the implementation are refused — the implementation whatever they hold,
    /// audited — and done by AHDA: the Department Manager starts the review, an internal Project Manager implements on their own
    /// project. A project the entity user does not manage shows them nothing.
    /// </summary>
    [Fact]
    public async Task EntityProjectManagersRaiseChangeRequestsButReviewAndImplementationRemainAhdas()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        await host.BaselinedAsync(client, sessions, projectId);
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 4));
        await client.OkOrFailAsync(sessions.EntityManager, $"{ChangeRequestDriver.Requests}/{requestId}/preview-materiality");
        await client.CommandOrFailAsync(sessions.EntityManager, requestId, "submit");

        using (HttpResponseMessage review = await client.CommandAsync(sessions.EntityManager, requestId, "start-review"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
        }

        await client.CommandOrFailAsync(sessions.DepartmentManager, requestId, "start-review");
        await host.DecideAndDeliverAsync("ChangeRequest", "ChangeRequest", requestId, ApprovalTaskDecision.Approve);
        using (HttpResponseMessage implement = await client.CommandAsync(sessions.EntityManager, requestId, "start-implementation"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, implement.StatusCode);
        }

        Assert.Equal(
            [$"ChangeRequest.AuthorityRefused DENIED {ChangeRequestDriver.Person(8)} CHANGE_REQUEST_IMPLEMENT EXTERNAL_USER"],
            await host.Database.QueryAsync($"""
                SELECT e.event_type || ' ' || e.outcome || ' ' || e.actor_user_id || ' '
                       || (SELECT a.new_value FROM audit_activity.audit_event_attribute a WHERE a.audit_event_id = e.id AND a.attribute_name = 'permission') || ' '
                       || (SELECT a.new_value FROM audit_activity.audit_event_attribute a WHERE a.audit_event_id = e.id AND a.attribute_name = 'reason')
                FROM audit_activity.audit_event e WHERE e.subject_id = '{requestId}' AND e.event_type = 'ChangeRequest.AuthorityRefused'
                """));
        JsonObject followed = await client.RequestAsync(sessions.EntityManager, requestId);
        Assert.Equal(("APPROVED", 4), (followed.Text("status"), followed["scheduleImpactDays"]!.GetValue<int>()));

        // An internal Project Manager implements on the project they manage; the entity user sees nothing of it.
        Guid internalProject = await host.ProjectAsync("LIGHT", projectManager: 5);
        Guid internalRequest = await client.RaiseAsync(sessions.InternalManager, ChangeRequestDriver.RequestBody(internalProject, "SCOPE", scopeImpact: "Drop the car park."));
        await host.ApprovedAsync(client, sessions, internalRequest, requester: sessions.InternalManager);
        Assert.Equal("IMPLEMENTATION", (await client.CommandOrFailAsync(sessions.InternalManager, internalRequest, "start-implementation")).Text("status"));

        using (HttpResponseMessage hidden = await client.GetAsync($"{ChangeRequestDriver.Requests}/{internalRequest}", sessions.EntityManager))
        {
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        JsonObject empty = await client.GetOrFailAsync(sessions.EntityManager, $"{ChangeRequestDriver.Requests}?projectId={internalProject}");
        Assert.Equal(0, empty["totalCount"]!.GetValue<int>());
        JsonObject own = await client.GetOrFailAsync(sessions.EntityManager, $"{ChangeRequestDriver.Requests}?projectId={projectId}&status=APPROVED");
        Assert.Equal([requestId.ToString()], own["items"]!.AsArray().Select(i => i!.Text("id")));
    }
}
