using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Domain.Approval;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// TASK-041's lifecycle through the real API and WF-11: DRAFT → SUBMITTED → UNDER_REVIEW → RETURNED or APPROVED_PLANNED,
/// then the Planned → Active command. Every change is a command or the review's outcome; a date reached and every
/// scheduled worker change nothing.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class RegistrationLifecycleTests(ProjectTestHost host)
{
    /// <summary>
    /// ADR-013: the entity fills in the information and AHDA approves. The draft has no Formal Project ID; AHDA's approval
    /// issues it; only the activation command makes the project ACTIVE (acceptance criterion 2).
    /// </summary>
    [Fact]
    public async Task AnEntityDraftIsApprovedByAhdaAndBecomesActiveOnlyByTheCommand()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();

        JsonObject draft = await client.CreateOrFailAsync(sessions.Entity, host.Registration());
        Guid projectId = AdministrationApi.IdOf(draft);
        Assert.Equal(("DRAFT", null, 1, "12500000.00"), (draft.Status(), draft.FormalProjectId(), draft["revisionNo"]!.GetValue<int>(), draft["registrationBudgetSar"]!.GetValue<string>()));
        Assert.Null(draft["projectManagerUserId"]);

        JsonObject submitted = await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(8) });
        Assert.Equal(("SUBMITTED", ProjectDriver.Person(8).ToString()), (submitted.Status(), submitted["projectManagerUserId"]!.GetValue<string>()));

        JsonObject underReview = await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review");
        Assert.Equal("UNDER_REVIEW", underReview.Status());
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync(projectId));
        Assert.Equal(
            ("Project", "Project", 1, "PROJECT_REGISTRATION", ProjectDriver.Person(3), ApprovalInstanceStatus.Pending),
            (run.Subject.Module, run.Subject.Type, run.Subject.RevisionNo, run.RoutingKey, run.RequestedByUserId, run.Status));

        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);

        JsonObject approved = await GetAsync(client, sessions.Entity, projectId);
        Assert.Equal("APPROVED_PLANNED", approved.Status());
        Assert.Matches("^PRJ-[0-9]{6,}$", approved.FormalProjectId());
        Assert.Null(approved["activatedAt"]);

        // ADR-009: WF-03's ACTIVE baseline is the activation precondition (schedule-baseline.md D-11).
        await ScheduleFixture.ActiveBaselineAsync(host.Database, projectId);
        JsonObject active = await client.CommandOrFailAsync(sessions.Approver, projectId, "activate");
        Assert.Equal(("ACTIVE", approved.FormalProjectId()), (active.Status(), active.FormalProjectId()));
        Assert.NotNull(active["activatedAt"]);

        using HttpResponseMessage again = await client.PostAsync($"{ProjectDriver.Projects}/{projectId}/activate", sessions.Approver);
        Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), (again.StatusCode, await again.CodeOfAsync()));

        Assert.Equal(
            ["Project.ProjectActivated", "Project.ProjectCreated", "Project.ProjectSubmitted", "Project.RegistrationApproved", "Project.ReviewStarted"],
            (await host.AuditEventsAsync(projectId)).Order());
    }

    /// <summary>
    /// The workbook's invariant: reaching the planned start and end dates, and every scheduled worker running, never
    /// activates or completes a project. Only the command does (acceptance criterion 2).
    /// </summary>
    [Fact]
    public async Task ADateReachedAndEveryWorkerRunNeverActivateAProject()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.UnderReviewAsync(client, sessions, startsInDays: 1);
        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);
        JsonObject before = await host.RowAsync(projectId);

        host.Clock.Advance(TimeSpan.FromDays(800));
        try
        {
            await host.RunWorkersAsync();
            await host.RunWorkersAsync();
            Assert.Equal("APPROVED_PLANNED", (await GetAsync(client, (await client.SignInOrFailAsync(2)).AccessToken, projectId)).Status());
        }
        finally
        {
            host.Clock.Reset();
        }

        Assert.Equal(before.ToJsonString(), (await host.RowAsync(projectId)).ToJsonString());
    }

    /// <summary>TASK-035 D-9 as WF-01 uses it: a RETURNED registration is edited and resubmitted as revision 2, which a new linked run reviews.</summary>
    [Fact]
    public async Task AReturnedRegistrationComesBackAsTheNextRevisionUnderANewRun()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.UnderReviewAsync(client, sessions);

        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Return, "Budget breakdown missing");
        using HttpResponseMessage returned = await client.GetAsync($"{ProjectDriver.Projects}/{projectId}", sessions.Entity);
        JsonObject body = await returned.ReadObjectAsync();
        Assert.Equal(("RETURNED", null, 1), (body.Status(), body.FormalProjectId(), body["revisionNo"]!.GetValue<int>()));

        JsonObject revised = host.Registration(change: r => r["registrationBudgetSar"] = "13000000.00");
        using (HttpResponseMessage edited = await client.PutAsync($"{ProjectDriver.Projects}/{projectId}", sessions.Entity, revised, AdministrationApi.ETagOf(returned)))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        JsonObject resubmitted = await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(8) });
        Assert.Equal(("SUBMITTED", 2), (resubmitted.Status(), resubmitted["revisionNo"]!.GetValue<int>()));
        await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review");

        IReadOnlyList<ApprovalInstanceDetail> runs = await host.RunsAsync(projectId);
        Assert.Equal([(1, ApprovalInstanceStatus.Returned), (2, ApprovalInstanceStatus.Pending)], runs.Select(r => (r.Subject.RevisionNo, r.Status)));
        Assert.Equal(runs[0].Id, runs[1].PreviousInstanceId);

        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);
        JsonObject approved = await GetAsync(client, sessions.Entity, projectId);
        Assert.Equal(("APPROVED_PLANNED", "13000000.00"), (approved.Status(), approved["registrationBudgetSar"]!.GetValue<string>()));
    }

    /// <summary>
    /// The workbook names RETURNED and APPROVED_PLANNED as review's only ends: a rejection, and the reviewer withdrawing the
    /// run, send the registration back to its registrant too; neither issues an identifier (ERD E-3, record F-4).
    /// </summary>
    [Theory]
    [InlineData(ApprovalOutcomeDecision.Rejected)]
    [InlineData(ApprovalOutcomeDecision.Withdrawn)]
    public async Task ARejectedOrWithdrawnReviewReturnsTheRegistration(ApprovalOutcomeDecision decision)
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.UnderReviewAsync(client, sessions);

        if (decision == ApprovalOutcomeDecision.Rejected)
        {
            await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Reject, "Out of programme scope");
        }
        else
        {
            // The run's requester is the reviewer who started it.
            ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync(projectId));
            using HttpResponseMessage withdrawn = await client.PostAsync($"/api/v1/approval-instances/{run.Id}/withdraw", sessions.Reviewer);
            Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
            Assert.True(await host.DeliverAsync(run.Id));
        }

        JsonObject project = await GetAsync(client, sessions.Entity, projectId);
        Assert.Equal(("RETURNED", null), (project.Status(), project.FormalProjectId()));
    }

    /// <summary>
    /// EV-4 and EV-5: the handler applies an outcome only to the revision under review. The same outcome handed to it again
    /// — past the framework's own exactly-once guard — changes nothing, and is audited as ignored.
    /// </summary>
    [Fact]
    public async Task AnOutcomeHandedToTheProjectTwiceIsAppliedOnce()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.UnderReviewAsync(client, sessions);
        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);
        JsonObject applied = await host.RowAsync(projectId);
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync(projectId));
        string payload = Assert.Single(await host.Database.QueryAsync(
            $"SELECT payload::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{run.Id}-outcome'"));

        await host.WithScopeAsync(async services =>
        {
            IApprovalOutcomeHandler handler = services.GetServices<IApprovalOutcomeHandler>().Single(h => h.SubjectModule == "Project");
            await handler.HandleAsync(EventSerialization.Deserialize<ApprovalOutcomeRecorded>(payload), CancellationToken.None);
            return true;
        });

        Assert.Equal(applied.ToJsonString(), (await host.RowAsync(projectId)).ToJsonString());
        Assert.Equal(1, (await host.AuditEventsAsync(projectId)).Count(e => e == "Project.RegistrationApproved"));
        Assert.Contains("Project.ApprovalOutcomeIgnored", await host.AuditEventsAsync(projectId));
    }

    /// <summary>A submission withdrawn before review goes back to DRAFT without a manager; a DRAFT is deleted only by whoever created it.</summary>
    [Fact]
    public async Task ASubmissionIsWithdrawnAndOnlyItsCreatorDeletesTheDraft()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        JsonObject registration = host.Registration(change: r =>
        {
            r["externalEntityId"] = null;
            r["participationMode"] = "AHDA_MANAGED";
        });
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Reviewer, registration));
        await client.CommandOrFailAsync(sessions.Reviewer, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(5) });

        using (HttpResponseMessage submittedDelete = await client.DeleteProjectAsync(projectId, sessions.Reviewer))
        {
            Assert.Equal((HttpStatusCode.Conflict, "PROJECT_NOT_EDITABLE"), (submittedDelete.StatusCode, await submittedDelete.CodeOfAsync()));
        }

        JsonObject withdrawn = await client.CommandOrFailAsync(sessions.Reviewer, projectId, "withdraw");
        Assert.Equal("DRAFT", withdrawn.Status());
        Assert.Null(withdrawn["projectManagerUserId"]);

        // local.r02 sees the draft but did not create it.
        using (HttpResponseMessage notOwner = await client.DeleteProjectAsync(projectId, (await client.SignInOrFailAsync(2)).AccessToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, notOwner.StatusCode);
        }

        using (HttpResponseMessage deleted = await client.DeleteProjectAsync(projectId, sessions.Reviewer))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        using (HttpResponseMessage again = await client.DeleteProjectAsync(projectId, sessions.Reviewer))
        {
            Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        }

        Assert.Empty(await host.Database.QueryAsync($"SELECT id::text FROM project.project WHERE id = '{projectId}'"));
        Assert.Contains("Project.ProjectDeleted", await host.AuditEventsAsync(projectId));
    }

    private static async Task<JsonObject> GetAsync(HttpClient client, string token, Guid projectId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{ProjectDriver.Projects}/{projectId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadObjectAsync();
    }
}
