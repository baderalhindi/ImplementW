using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Approval;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ChangeRequest;

/// <summary>
/// The row's lifecycle through the real API, WF-11 and the outbox (TASK-060): Draft → Submitted → Under Review → Returned / Approved /
/// Rejected → Implementation → Implemented → Closed, and Withdrawn — each step by its command or WF-11's decision, and no other.
/// </summary>
[Collection(ChangeRequestSuite.Name)]
public sealed class ChangeRequestLifecycleTests(ChangeRequestTestHost host)
{
    /// <summary>
    /// A returned request is corrected and resubmitted as revision 2 under a new run and a new evaluation; once approved its fields no
    /// longer change; it is implemented only once its authorisation has been applied, and once closed it takes no command.
    /// </summary>
    [Fact]
    public async Task AReturnedRequestComesBackAsTheNextRevisionAndIsImplementedOnlyOnceItsAuthorizationIsApplied()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        await host.BaselinedAsync(client, sessions, projectId);
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 5));

        await client.UnderReviewAsync(sessions, requestId);
        await host.DecideAndDeliverAsync("ChangeRequest", "ChangeRequest", requestId, ApprovalTaskDecision.Return);
        Assert.Equal(("RETURNED", 1), Revision(await client.RequestAsync(sessions.EntityManager, requestId)));

        using (HttpResponseMessage current = await client.GetAsync($"{ChangeRequestDriver.Requests}/{requestId}", sessions.EntityManager))
        using (HttpResponseMessage corrected = await client.PutAsync($"{ChangeRequestDriver.Requests}/{requestId}", sessions.EntityManager, Fields(6), AdministrationApi.ETagOf(current)))
        {
            Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        }

        Assert.Equal(("SUBMITTED", 2), Revision(await client.CommandOrFailAsync(sessions.EntityManager, requestId, "submit")));
        JsonObject reviewed = await client.CommandOrFailAsync(sessions.Officer, requestId, "start-review");
        Assert.Equal(("UNDER_REVIEW", 2, 2), (reviewed.Text("status"), reviewed["revisionNo"]!.GetValue<int>(), reviewed["materiality"]!["revisionNo"]!.GetValue<int>()));
        await host.DecideAndDeliverAsync("ChangeRequest", "ChangeRequest", requestId, ApprovalTaskDecision.Approve);
        Assert.Equal(
            [$"1 {ApprovalInstanceStatus.Returned}", $"2 {ApprovalInstanceStatus.Approved}"],
            (await host.RunsAsync("ChangeRequest", "ChangeRequest", requestId)).Select(r => $"{r.Subject.RevisionNo} {r.Status}"));
        Assert.Equal(["1", "2"], await host.Database.QueryAsync($"SELECT revision_no::text FROM change_request.materiality_evaluation WHERE change_request_id = '{requestId}' ORDER BY revision_no"));

        using (HttpResponseMessage current = await client.GetAsync($"{ChangeRequestDriver.Requests}/{requestId}", sessions.EntityManager))
        using (HttpResponseMessage late = await client.PutAsync($"{ChangeRequestDriver.Requests}/{requestId}", sessions.EntityManager, Fields(30), AdministrationApi.ETagOf(current)))
        {
            Assert.Equal((HttpStatusCode.Conflict, "CHANGE_REQUEST_NOT_EDITABLE"), await late.RefusalAsync());
        }

        // Implementation: not implemented until WF-03 applies the authorisation.
        Guid authorization = ChangeRequestDriver.AuthorizationOf((await client.CommandOrFailAsync(sessions.Officer, requestId, "start-implementation"))["authorizations"]!.AsArray(), "REBASELINE");
        using (HttpResponseMessage early = await client.CommandAsync(sessions.Officer, requestId, "mark-implemented"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "CHANGE_REQUEST_AUTHORIZATION_PENDING"), await early.RefusalAsync());
        }

        await client.SubmittedBaselineAsync(sessions.EntityManager, projectId, authorization);
        JsonObject implemented = await client.CommandOrFailAsync(sessions.Officer, requestId, "mark-implemented");
        Assert.Equal(("IMPLEMENTED", "APPLIED"), (implemented.Text("status"), implemented["authorizations"]![0]!.Text("status")));
        Assert.NotNull(implemented["implementedAt"]);
        Assert.Equal("CLOSED", (await client.CommandOrFailAsync(sessions.Officer, requestId, "close")).Text("status"));
        using (HttpResponseMessage closed = await client.CommandAsync(sessions.EntityManager, requestId, "submit"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "TERMINAL_STATE"), await closed.RefusalAsync());
        }

        Assert.Equal(
            ["ChangeRequest.ChangeRequestCreated", "ChangeRequest.ChangeRequestSubmitted", "ChangeRequest.MaterialityEvaluated", "ChangeRequest.ReviewStarted",
             "ChangeRequest.ChangeRequestReturned", "ChangeRequest.ChangeRequestChanged", "ChangeRequest.ChangeRequestSubmitted", "ChangeRequest.MaterialityEvaluated",
             "ChangeRequest.ReviewStarted", "ChangeRequest.ChangeRequestApproved", "ChangeRequest.ChangeAuthorizationIssued", "ChangeRequest.ImplementationStarted",
             "ChangeRequest.ChangeAuthorizationApplied", "ChangeRequest.ChangeRequestImplemented", "ChangeRequest.ChangeRequestClosed"],
            await host.AuditEventsAsync("ChangeRequest", requestId));
    }

    /// <summary>
    /// Rejected and Withdrawn are final (WF-08 BR-CHG-015): a rejection issues nothing; the requester withdraws a request not under
    /// review, and one under review by withdrawing its WF-11 run; only a DRAFT never submitted is deleted.
    /// </summary>
    [Fact]
    public async Task RejectionAndWithdrawalAreFinal()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        await host.BaselinedAsync(client, sessions, projectId);

        Guid rejected = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 5));
        await client.UnderReviewAsync(sessions, rejected);
        await host.DecideAndDeliverAsync("ChangeRequest", "ChangeRequest", rejected, ApprovalTaskDecision.Reject);
        JsonObject rejection = await client.RequestAsync(sessions.EntityManager, rejected);
        Assert.Equal(("REJECTED", 0), (rejection.Text("status"), rejection["authorizations"]!.AsArray().Count));
        using (HttpResponseMessage again = await client.CommandAsync(sessions.EntityManager, rejected, "submit"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "TERMINAL_STATE"), await again.RefusalAsync());
        }

        Guid submitted = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 5));
        await client.CommandOrFailAsync(sessions.EntityManager, submitted, "submit");
        Assert.Equal("WITHDRAWN", (await client.CommandOrFailAsync(sessions.EntityManager, submitted, "withdraw")).Text("status"));

        Guid underReview = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 5));
        await client.UnderReviewAsync(sessions, underReview);
        using (HttpResponseMessage direct = await client.CommandAsync(sessions.EntityManager, underReview, "withdraw"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "CHANGE_REQUEST_UNDER_REVIEW"), await direct.RefusalAsync());
        }

        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync("ChangeRequest", "ChangeRequest", underReview));
        AdministrationResult<ApprovalInstanceDetail> withdrawn = await host.WithScopeAsync(services =>
            services.GetRequiredService<IApprovalWorkflowService>().WithdrawAsync(ChangeRequestDriver.Person(8), run.Id, CancellationToken.None));
        Assert.True(withdrawn.Succeeded, $"Withdrawal refused: {withdrawn.Error}");
        Assert.True(await host.DeliverAsync(run.Id));
        Assert.Equal("WITHDRAWN", (await client.RequestAsync(sessions.EntityManager, underReview)).Text("status"));

        Guid draft = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 5));
        using (HttpResponseMessage deleted = await client.SendAsync(HttpMethod.Delete, $"{ChangeRequestDriver.Requests}/{draft}", sessions.EntityManager))
        using (HttpResponseMessage gone = await client.GetAsync($"{ChangeRequestDriver.Requests}/{draft}", sessions.EntityManager))
        using (HttpResponseMessage kept = await client.SendAsync(HttpMethod.Delete, $"{ChangeRequestDriver.Requests}/{submitted}", sessions.EntityManager))
        {
            Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NotFound), (deleted.StatusCode, gone.StatusCode));
            Assert.Equal((HttpStatusCode.Conflict, "CHANGE_REQUEST_NOT_EDITABLE"), await kept.RefusalAsync());
        }

        Assert.Equal(["ChangeRequest.ChangeRequestCreated", "ChangeRequest.ChangeRequestDeleted"], await host.AuditEventsAsync("ChangeRequest", draft));
    }

    /// <summary>
    /// The originator is the WF-11 run's requester, whoever starts the review, so WF-11 keeps them from approving their own change
    /// (WF-08 CHG-GP-06): AHDA's officer who raised a request cannot decide it.
    /// </summary>
    [Fact]
    public async Task NoOneApprovesTheirOwnChange()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        await host.BaselinedAsync(client, sessions, projectId);
        Guid requestId = await client.RaiseAsync(sessions.Officer, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 5));
        await client.UnderReviewAsync(sessions, requestId, requester: sessions.Officer);

        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync("ChangeRequest", "ChangeRequest", requestId));
        Assert.Equal(ChangeRequestDriver.Person(2), run.RequestedByUserId);
        AdministrationResult<ApprovalInstanceDetail> own = await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
            ChangeRequestDriver.Person(2), Assert.Single(run.Tasks).Id, ApprovalTaskDecision.Approve, null, CancellationToken.None));
        Assert.False(own.Succeeded);
        Assert.Equal("UNDER_REVIEW", (await client.RequestAsync(sessions.Officer, requestId)).Text("status"));
    }

    private static (string Status, int RevisionNo) Revision(JsonObject request) => (request.Text("status"), request["revisionNo"]!.GetValue<int>());

    private static object Fields(int scheduleImpactDays) => new
    {
        title = new { text = "Change of schedule", language = "en" },
        justification = new { text = "The ground survey found rock where the design assumed sand.", language = "en" },
        scheduleImpactDays,
    };
}
