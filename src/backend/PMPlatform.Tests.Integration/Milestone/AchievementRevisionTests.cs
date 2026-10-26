using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Milestone;

/// <summary>
/// TASK-050's acceptance criteria through the real API, WF-11 and the outbox: one shared milestone row, and an achievement whose
/// correction is always a new revision, the accepted one retained and marked SUPERSEDED, never overwritten in place.
/// </summary>
[Collection(MilestoneSuite.Name)]
public sealed class AchievementRevisionTests(MilestoneTestHost host)
{
    /// <summary>
    /// The workbook's validation check: submit and accept a milestone achievement, then submit a correction; the original accepted
    /// revision stays queryable and unmodified while the new revision becomes current. Both revisions name the one milestone row.
    /// </summary>
    [Fact]
    public async Task ACorrectionIsANewRevisionAndTheAcceptedOneIsKeptUnmodified()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, projectId);
        DateOnly claimed = MilestoneDriver.Today.AddDays(-10);

        // The entity Project Manager claims (ADR-013); WF-11 routes it to R02, who accepts.
        JsonObject first = await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, claimed);
        Guid firstId = AdministrationApi.IdOf(first);
        Assert.Equal(("SUBMITTED", 1, MilestoneDriver.Person(8).ToString()), (first.Text("status"), first["revisionNo"]!.GetValue<int>(), first.Text("submittedByUserId")));
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync(firstId));
        Assert.Equal((MilestoneApprovalRouting.AchievementRoutingKey, 1), (run.RoutingKey, run.Subject.RevisionNo));
        await host.DecideAndDeliverAsync(firstId, ApprovalTaskDecision.Approve);

        JsonObject accepted = await client.AchievementAsync(sessions.ProjectManager, firstId);
        Assert.Equal(("ACCEPTED", true, MilestoneDriver.Iso(claimed), MilestoneDriver.Person(2).ToString()),
            (accepted.Text("status"), accepted["isCurrent"]!.GetValue<bool>(), accepted.Text("acceptedActualAchievementDate"), accepted.Text("reviewedByUserId")));
        Assert.Equal("ACHIEVED", (await client.MilestoneOrFailAsync(sessions.ProjectManager, milestoneId)).Text("status"));
        string acceptedRow = await host.RowAsync(firstId, "status", "superseded_by_achievement_id", "updated_at", "updated_by");

        // The correction: a new revision of the same milestone, with another date. The accepted one stays current meanwhile.
        DateOnly corrected = claimed.AddDays(-3);
        JsonObject correction = await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, corrected);
        Guid correctionId = AdministrationApi.IdOf(correction);
        Assert.Equal(2, correction["revisionNo"]!.GetValue<int>());
        Assert.Equal(["1 ACCEPTED", "2 SUBMITTED"], await host.RevisionStatesAsync(milestoneId));
        await host.DecideAndDeliverAsync(correctionId, ApprovalTaskDecision.Approve);

        Assert.Equal(["1 SUPERSEDED", "2 ACCEPTED"], await host.RevisionStatesAsync(milestoneId));
        JsonObject original = await client.AchievementAsync(sessions.ProjectManager, firstId);
        JsonObject current = await client.AchievementAsync(sessions.ProjectManager, correctionId);
        Assert.Equal(("SUPERSEDED", false, correctionId.ToString(), MilestoneDriver.Iso(claimed)),
            (original.Text("status"), original["isCurrent"]!.GetValue<bool>(), original.Text("supersededByAchievementId"), original.Text("acceptedActualAchievementDate")));
        Assert.Equal(("ACCEPTED", true, MilestoneDriver.Iso(corrected)), (current.Text("status"), current["isCurrent"]!.GetValue<bool>(), current.Text("acceptedActualAchievementDate")));

        // Unmodified: every column of the original but its status and the link to its successor is as it was when accepted.
        Assert.Equal(acceptedRow, await host.RowAsync(firstId, "status", "superseded_by_achievement_id", "updated_at", "updated_by"));

        // One shared milestone row, named by both revisions; WF-05 holds no copy of it.
        Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM schedule.project_milestone WHERE project_id = '{projectId}'"));
        Assert.Equal([milestoneId.ToString()], await host.Database.QueryAsync($"SELECT DISTINCT project_milestone_id::text FROM milestone.milestone_achievement WHERE project_id = '{projectId}'"));

        Assert.Equal([correctionId.ToString(), firstId.ToString()],
            (await client.AchievementPageAsync(sessions.ProjectManager, $"projectMilestoneId={milestoneId}")).Select(i => i!.Text("id")));
        Assert.Equal(
            ["Milestone.AchievementStarted", "Milestone.AchievementSubmitted", "Milestone.AchievementAccepted", "Milestone.AchievementSuperseded"],
            await host.AuditEventsAsync("Milestone", firstId));
        Assert.Equal(["Schedule.MilestoneCreated", "Schedule.MilestoneAchieved"], await host.AuditEventsAsync("Schedule", milestoneId));
    }

    /// <summary>TASK-035 D-9 for WF-05: a returned claim keeps the approver's reason, and the next claim is a new revision under a new run.</summary>
    [Fact]
    public async Task AReturnedClaimKeepsItsReasonAndTheNextClaimIsANewRevision()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager));
        Guid firstId = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));

        await host.DecideAndDeliverAsync(firstId, ApprovalTaskDecision.Return, "Attach the handover minutes");

        JsonObject returned = await client.AchievementAsync(sessions.ProjectManager, firstId);
        Assert.Equal(("RETURNED", "Attach the handover minutes", null), (returned.Text("status"), returned["returnReason"]!.Text("text"), returned["acceptedActualAchievementDate"]));
        Assert.Equal("PLANNED", (await client.MilestoneOrFailAsync(sessions.ProjectManager, milestoneId)).Text("status"));

        using (HttpResponseMessage again = await client.PostAsync($"{MilestoneDriver.Achievements}/{firstId}/submit", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }

        Guid secondId = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));
        Assert.Equal(["1 RETURNED", "2 SUBMITTED"], await host.RevisionStatesAsync(milestoneId));
        Assert.Equal(2, Assert.Single(await host.RunsAsync(secondId)).Subject.RevisionNo);
    }

    /// <summary>The ERD's value set has no REJECTED: a rejected claim goes back to its claimant as RETURNED, with the reason; the decision stays on the audit event.</summary>
    [Fact]
    public async Task ARejectedClaimIsReturned()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager));
        Guid achievementId = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));

        await host.DecideAndDeliverAsync(achievementId, ApprovalTaskDecision.Reject, "Not yet achieved");

        Assert.Equal(["1 RETURNED"], await host.RevisionStatesAsync(milestoneId));
        Assert.Equal(["REJECTED"], await host.Database.QueryAsync($"""
            SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
            WHERE e.subject_id = '{achievementId}' AND e.event_type = 'Milestone.AchievementReturned' AND a.attribute_name = 'decision'
            """));
    }

    /// <summary>
    /// A milestone cancelled in the schedule while a claim of it is with WF-11 cannot become ACHIEVED: the approval is applied as a
    /// return, and the milestone stays CANCELLED.
    /// </summary>
    [Fact]
    public async Task AnApprovalForAMilestoneCancelledMeanwhileReturnsTheClaim()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager));
        Guid achievementId = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Milestones}/{milestoneId}/cancel");

        await host.DecideAndDeliverAsync(achievementId, ApprovalTaskDecision.Approve);

        Assert.Equal(["1 RETURNED"], await host.RevisionStatesAsync(milestoneId));
        Assert.Equal("CANCELLED", (await client.MilestoneOrFailAsync(sessions.ProjectManager, milestoneId)).Text("status"));
        using HttpResponseMessage claim = await client.PostAsync(MilestoneDriver.Achievements, sessions.ProjectManager, MilestoneDriver.Claim(milestoneId, MilestoneDriver.Today));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, MilestoneErrorCodes.NotClaimable), await claim.RefusalAsync());
    }

    /// <summary>EV-4, EV-5: an outcome delivered again, or for a revision no longer under review, changes nothing and is audited as ignored.</summary>
    [Fact]
    public async Task AnOutcomeAppliedTwiceChangesNothingTheSecondTime()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager));
        Guid achievementId = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));
        await host.DecideAndDeliverAsync(achievementId, ApprovalTaskDecision.Approve);
        string row = await host.RowAsync(achievementId);

        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync(achievementId));
        string payload = Assert.Single(await host.Database.QueryAsync(
            $"SELECT payload::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{run.Id}-outcome'"));
        await host.WithScopeAsync(async services =>
        {
            IApprovalOutcomeHandler handler = services.GetServices<IApprovalOutcomeHandler>().Single(h => h.SubjectModule == MilestoneApprovalRouting.SubjectModule);
            await handler.HandleAsync(EventSerialization.Deserialize<ApprovalOutcomeRecorded>(payload), CancellationToken.None);
            return true;
        });

        Assert.Equal(row, await host.RowAsync(achievementId));
        Assert.Equal("Milestone.ApprovalOutcomeIgnored", (await host.AuditEventsAsync("Milestone", achievementId))[^1]);
    }

    /// <summary>One revision of a milestone is on its way at a time; a DRAFT is edited and deleted, a submitted one is not.</summary>
    [Fact]
    public async Task OneOpenRevisionAtATimeAndOnlyADraftChanges()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager));

        using HttpResponseMessage created = await client.PostAsync(MilestoneDriver.Achievements, sessions.ProjectManager, MilestoneDriver.Claim(milestoneId, MilestoneDriver.Today));
        Guid draftId = AdministrationApi.IdOf(await created.ReadObjectAsync());
        using (HttpResponseMessage second = await client.PostAsync(MilestoneDriver.Achievements, sessions.ProjectManager, MilestoneDriver.Claim(milestoneId, MilestoneDriver.Today)))
        {
            Assert.Equal((HttpStatusCode.Conflict, MilestoneErrorCodes.AchievementOpen), await second.RefusalAsync());
        }

        object changes = new { claimedAchievementDate = MilestoneDriver.Iso(MilestoneDriver.Today.AddDays(-1)), narrative = (object?)null };
        using (HttpResponseMessage unconditional = await client.PutAsync($"{MilestoneDriver.Achievements}/{draftId}", sessions.ProjectManager, changes, null))
        {
            Assert.Equal(HttpStatusCode.PreconditionRequired, unconditional.StatusCode);
        }

        using HttpResponseMessage edited = await client.PutAsync($"{MilestoneDriver.Achievements}/{draftId}", sessions.ProjectManager, changes, AdministrationApi.ETagOf(created));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        JsonObject draft = await edited.ReadObjectAsync();
        Assert.Equal((MilestoneDriver.Iso(MilestoneDriver.Today.AddDays(-1)), null), (draft.Text("claimedAchievementDate"), draft["narrative"]));

        using (HttpResponseMessage deleted = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{MilestoneDriver.Achievements}/{draftId}") { Headers = { { "Authorization", $"Bearer {sessions.ProjectManager}" } } }))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        Assert.Empty(await host.RevisionStatesAsync(milestoneId));
        Guid submittedId = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));
        Assert.Equal(["1 SUBMITTED"], await host.RevisionStatesAsync(milestoneId));
        JsonObject submitted = await client.AchievementAsync(sessions.ProjectManager, submittedId);
        using (HttpResponseMessage locked = await client.PutAsync($"{MilestoneDriver.Achievements}/{submittedId}", sessions.ProjectManager, changes, null))
        {
            Assert.Equal(HttpStatusCode.PreconditionRequired, locked.StatusCode);
        }

        using HttpResponseMessage current = await client.GetAsync($"{MilestoneDriver.Achievements}/{submittedId}", sessions.ProjectManager);
        using (HttpResponseMessage locked = await client.PutAsync($"{MilestoneDriver.Achievements}/{submittedId}", sessions.ProjectManager, changes, AdministrationApi.ETagOf(current)))
        {
            Assert.Equal((HttpStatusCode.Conflict, MilestoneErrorCodes.AchievementNotEditable), await locked.RefusalAsync());
        }

        using (HttpResponseMessage kept = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{MilestoneDriver.Achievements}/{submittedId}") { Headers = { { "Authorization", $"Bearer {sessions.ProjectManager}" } } }))
        {
            Assert.Equal((HttpStatusCode.Conflict, MilestoneErrorCodes.AchievementNotEditable), await kept.RefusalAsync());
        }

        Assert.Equal(submitted.ToJsonString(), (await client.AchievementAsync(sessions.ProjectManager, submittedId)).ToJsonString());
    }

    /// <summary>A claim is made on an ACTIVE project, for a milestone not cancelled, of a date not after today.</summary>
    [Fact]
    public async Task AClaimIsRefusedForAFutureDateOrAProjectNotActive()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager));
        using (HttpResponseMessage future = await client.PostAsync(MilestoneDriver.Achievements, sessions.ProjectManager, MilestoneDriver.Claim(milestoneId, MilestoneDriver.Today.AddDays(2))))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, MilestoneErrorCodes.ClaimedDateInvalid), await future.RefusalAsync());
        }

        Guid plannedMilestone = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager, state: "APPROVED_PLANNED"));
        using (HttpResponseMessage notActive = await client.PostAsync(MilestoneDriver.Achievements, sessions.ProjectManager, MilestoneDriver.Claim(plannedMilestone, MilestoneDriver.Today)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, MilestoneErrorCodes.ProjectNotActive), await notActive.RefusalAsync());
        }

        using HttpResponseMessage unknown = await client.PostAsync(MilestoneDriver.Achievements, sessions.ProjectManager, MilestoneDriver.Claim(Guid.NewGuid(), MilestoneDriver.Today));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, MilestoneErrorCodes.NotClaimable), await unknown.RefusalAsync());
    }

    /// <summary>
    /// ADR-013: the entity Project Manager claims on their own project only; acceptance stays with WF-05 through WF-11, where they
    /// hold no authority, and the R02 approver holds no MILESTONE_SUBMIT. Another project's claims are invisible (R-47).
    /// </summary>
    [Fact]
    public async Task TheEntityProjectManagerClaimsButNeverAccepts()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager));
        Guid achievementId = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));

        using (HttpResponseMessage inbox = await client.GetAsync("/api/v1/approval-tasks", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.Forbidden, inbox.StatusCode);
        }

        using (HttpResponseMessage approverClaims = await client.PostAsync(MilestoneDriver.Achievements, sessions.Portfolio, MilestoneDriver.Claim(milestoneId, MilestoneDriver.Today)))
        {
            Assert.Equal(HttpStatusCode.Forbidden, approverClaims.StatusCode);
        }

        // local.r05, an internal Project Manager, does not manage this project: its claims do not exist for them.
        string other = (await client.SignInOrFailAsync(5)).AccessToken;
        using (HttpResponseMessage hidden = await client.GetAsync($"{MilestoneDriver.Achievements}/{achievementId}", other))
        {
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        Assert.Empty(await client.AchievementPageAsync(other, $"projectMilestoneId={milestoneId}"));
        Assert.Single(await client.AchievementPageAsync(sessions.Portfolio, $"projectMilestoneId={milestoneId}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("projectId=00000000-0000-4000-8000-000000000001&projectMilestoneId=00000000-0000-4000-8000-000000000001")]
    public async Task AListIsOfOneMilestoneOrOfOneProject(string query)
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        using HttpResponseMessage response = await client.GetAsync($"{MilestoneDriver.Achievements}?{query}", sessions.ProjectManager);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
