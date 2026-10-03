using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>
/// Baseline approval routed through WF-11 (TASK-046 description; ADR-003 §8.2 edges 21, 28), and ADR-015: approval for the
/// Standard and Full profiles only.
/// </summary>
[Collection(ScheduleSuite.Name)]
public sealed class BaselineApprovalTests(ScheduleTestHost host)
{
    /// <summary>
    /// A candidate is submitted to WF-11 under SCHEDULE_BASELINE, its plan frozen until the outcome; local.r02's approval,
    /// delivered through the outbox, makes it the project's ACTIVE baseline with its copy of the schedule, and unfreezes the plan.
    /// </summary>
    [Fact]
    public async Task ABaselineIsApprovedThroughWf11AndBecomesTheActiveBaseline()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid design = await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);
        Guid build = await client.ActivityAsync(sessions.ProjectManager, projectId, "2", 0, 5);
        await client.LinkOrFailAsync(sessions.ProjectManager, design, build);

        JsonObject submitted = await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId);
        Guid baselineId = AdministrationApi.IdOf(submitted);
        Assert.Equal(("SUBMITTED", "APPROVED", 1, ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(14))),
            (submitted.Text("status"), submitted.Text("baselineType"), submitted["versionNo"]!.GetValue<int>(), submitted.Text("baselineFinishDate")));
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync(baselineId));
        Assert.Equal((ScheduleApprovalRouting.BaselineRoutingKey, ScheduleDriver.Person(8)), (run.RoutingKey, run.RequestedByUserId));

        // Frozen: what WF-11 reviews is what will activate (the spec's SCH-CC-10), in the API and in the database.
        using (HttpResponseMessage frozen = await client.PostAsync(ScheduleDriver.Activities, sessions.ProjectManager, ScheduleDriver.Activity(projectId, "3", 0, 2)))
        {
            Assert.Equal((HttpStatusCode.Conflict, "SCHEDULE_NOT_EDITABLE"), await frozen.RefusalAsync());
        }

        PostgresException guarded = await Assert.ThrowsAsync<PostgresException>(() =>
            host.Database.ExecuteRolledBackAsync($"UPDATE schedule.schedule_activity SET requested_start_date = requested_start_date + 1 WHERE id = '{design}'"));
        Assert.Contains("frozen", guarded.MessageText, StringComparison.Ordinal);

        await host.DecideAndDeliverAsync(baselineId, ApprovalTaskDecision.Approve);

        Assert.Equal(["1 ACTIVE"], await host.BaselineStatesAsync(projectId));
        using (HttpResponseMessage copy = await client.GetAsync($"{ScheduleDriver.Baselines}/{baselineId}/baseline-activities", sessions.ProjectManager))
        {
            Assert.Equal(2, (await copy.ReadObjectAsync())["items"]!.AsArray().Count);
        }

        using (HttpResponseMessage network = await client.GetAsync($"{ScheduleDriver.Baselines}/{baselineId}/baseline-dependencies", sessions.ProjectManager))
        {
            JsonNode dependency = Assert.Single((await network.ReadObjectAsync())["items"]!.AsArray())!;
            Assert.Equal((design.ToString(), build.ToString(), "FS"), (dependency.Text("predecessorActivityId"), dependency.Text("successorActivityId"), dependency.Text("dependencyType")));
        }

        Assert.Equal(baselineId.ToString(), (await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.Schedules, projectId)).Single()!.Text("activeBaselineId"));
        await client.ActivityAsync(sessions.ProjectManager, projectId, "3", 0, 2);
        Assert.Equal(["Schedule.BaselineCreated", "Schedule.BaselineSubmitted", "Schedule.BaselineActivated"], await host.AuditEventsAsync(baselineId));
    }

    /// <summary>TASK-035 D-9: a returned candidate is corrected and resubmitted as revision 2, reviewed by a new run linked to the first.</summary>
    [Fact]
    public async Task AReturnedBaselineComesBackAsTheNextRevisionUnderANewRun()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid activity = await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);
        Guid baselineId = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId));

        await host.DecideAndDeliverAsync(baselineId, ApprovalTaskDecision.Return, "Sequence the works properly");
        Assert.Equal(["1 RETURNED"], await host.BaselineStatesAsync(projectId));

        // The plan is open again for the correction.
        using HttpResponseMessage current = await client.GetAsync($"{ScheduleDriver.Activities}/{activity}", sessions.ProjectManager);
        using (HttpResponseMessage corrected = await client.PutAsync($"{ScheduleDriver.Activities}/{activity}", sessions.ProjectManager,
                   new { wbsCode = "1", name = new { text = "Works", language = "en" }, requestedStartDate = ScheduleDriver.Iso(ScheduleDriver.Day1), plannedDurationDays = 12 },
                   AdministrationApi.ETagOf(current)))
        {
            Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        }

        JsonObject resubmitted = await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Baselines}/{baselineId}/submit");
        Assert.Equal(("SUBMITTED", 2, ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(11))),
            (resubmitted.Text("status"), resubmitted["revisionNo"]!.GetValue<int>(), resubmitted.Text("baselineFinishDate")));
        IReadOnlyList<ApprovalInstanceDetail> runs = await host.RunsAsync(baselineId);
        Assert.Equal([1, 2], runs.Select(r => r.Subject.RevisionNo));
        Assert.Equal(runs[0].Id, runs[1].PreviousInstanceId);

        await host.DecideAndDeliverAsync(baselineId, ApprovalTaskDecision.Approve);
        Assert.Equal(["1 ACTIVE"], await host.BaselineStatesAsync(projectId));
    }

    /// <summary>REJECTED by the approver and WITHDRAWN by the requester end the candidate; the project may open another.</summary>
    [Fact]
    public async Task ARejectedOrWithdrawnBaselineEndsAndAnotherMayBeOpened()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid rejectedProject = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        await client.ActivityAsync(sessions.ProjectManager, rejectedProject, "1", 0, 10);
        Guid rejected = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, rejectedProject));
        await host.DecideAndDeliverAsync(rejected, ApprovalTaskDecision.Reject, "Not this plan");

        Guid withdrawnProject = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        await client.ActivityAsync(sessions.ProjectManager, withdrawnProject, "1", 0, 10);
        Guid withdrawn = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, withdrawnProject));
        Guid run = Assert.Single(await host.RunsAsync(withdrawn)).Id;
        Assert.True((await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>()
            .WithdrawAsync(ScheduleDriver.Person(8), run, CancellationToken.None))).Succeeded);
        Assert.True(await host.DeliverAsync(run));

        Assert.Equal(["1 REJECTED"], await host.BaselineStatesAsync(rejectedProject));
        Assert.Equal(["1 WITHDRAWN"], await host.BaselineStatesAsync(withdrawnProject));
        using (HttpResponseMessage resubmit = await client.PostAsync($"{ScheduleDriver.Baselines}/{rejected}/submit", sessions.ProjectManager))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await resubmit.RefusalAsync());
        }

        Assert.Equal(2, (await client.CreatedOrFailAsync(sessions.ProjectManager, ScheduleDriver.Baselines, new { projectId = rejectedProject }))["versionNo"]!.GetValue<int>());
    }

    /// <summary>One candidate at a time per project; a DRAFT one is deleted (HARD_DRAFT), a submitted one is not.</summary>
    [Fact]
    public async Task AProjectHasOneCandidateAtATime()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        using (HttpResponseMessage empty = await client.PostAsync(ScheduleDriver.Baselines, sessions.ProjectManager, new { projectId }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_BASELINE_EMPTY"), await empty.RefusalAsync());
        }

        await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);
        Guid draft = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, ScheduleDriver.Baselines, new { projectId }));
        using (HttpResponseMessage second = await client.PostAsync(ScheduleDriver.Baselines, sessions.ProjectManager, new { projectId }))
        {
            Assert.Equal((HttpStatusCode.Conflict, "SCHEDULE_BASELINE_CANDIDATE_EXISTS"), await second.RefusalAsync());
        }

        using (HttpResponseMessage deleted = await client.SendAsync(HttpMethod.Delete, $"{ScheduleDriver.Baselines}/{draft}", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        Guid submitted = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId));
        using HttpResponseMessage notDeleted = await client.SendAsync(HttpMethod.Delete, $"{ScheduleDriver.Baselines}/{submitted}", sessions.ProjectManager);
        Assert.Equal((HttpStatusCode.Conflict, "SCHEDULE_BASELINE_NOT_EDITABLE"), await notDeleted.RefusalAsync());
    }

    /// <summary>ADR-015: under the Light profile a baseline needs no approval — it activates on submission, with no WF-11 run.</summary>
    [Fact]
    public async Task UnderALightProfileTheBaselineActivatesOnSubmissionWithoutWf11()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager, profileId: host.LightProfileId);
        await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);

        JsonObject activated = await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId);

        Assert.Equal("ACTIVE", activated.Text("status"));
        Assert.NotNull(activated["activatedAt"]);
        Assert.Empty(await host.RunsAsync(AdministrationApi.IdOf(activated)));
        Assert.Equal(["false"], await host.Database.QueryAsync($"""
            SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
            WHERE e.subject_id = '{AdministrationApi.IdOf(activated)}' AND e.event_type = 'Schedule.BaselineActivated' AND a.attribute_name = 'approval_required'
            """));
    }
}
