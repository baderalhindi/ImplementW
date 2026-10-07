using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Suspension;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Suspension;

/// <summary>
/// TASK-062's three acceptance criteria over HTTP, and the activation rules behind them: a second suspension request for a suspended
/// project is refused; resuming changes no baseline, and a rebaseline still needs WF-03's own action under a WF-08 authorisation; approval
/// and the lifecycle activation are two transactions with two audit events, by different actors.
/// </summary>
[Collection(SuspensionSuite.Name)]
public sealed class SuspensionLifecycleTests(SuspensionTestHost host)
{
    /// <summary>
    /// Validation check 1 and acceptance criterion 1: once the project is suspended, a second suspension request is refused 409
    /// SUSPENSION_ALREADY_EXISTS, and so is a second one while the first is still open; the project keeps one active suspension.
    /// </summary>
    [Fact]
    public async Task ASecondSuspensionRequestForAnAlreadySuspendedProjectIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();

        Guid first = await client.RaiseAsync(sessions.EntityManager, SuspensionDriver.RequestBody(projectId, "SUSPEND", SuspensionDriver.Today));
        using (HttpResponseMessage concurrent = await client.PostAsync(SuspensionDriver.Requests, sessions.Officer, SuspensionDriver.RequestBody(projectId, "SUSPEND", SuspensionDriver.Today)))
        {
            Assert.Equal((HttpStatusCode.Conflict, "SUSPENSION_ALREADY_EXISTS"), await concurrent.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.EntityManager, first, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, first, "start-review");
        await host.DecideAndDeliverAsync(first, ApprovalTaskDecision.Approve);
        await host.ActivateDueAsync();
        Assert.Equal("SUSPENDED", await host.LifecycleOfAsync(projectId));

        foreach (string token in new[] { sessions.EntityManager, sessions.Officer })
        {
            using HttpResponseMessage second = await client.PostAsync(SuspensionDriver.Requests, token, SuspensionDriver.RequestBody(projectId, "SUSPEND", SuspensionDriver.Today));
            Assert.Equal((HttpStatusCode.Conflict, "SUSPENSION_ALREADY_EXISTS"), await second.RefusalAsync());
        }

        Assert.Equal(["open"], await host.SuspensionPeriodsAsync(projectId));
        Assert.Equal(["EFFECTED"], await host.Database.QueryAsync($"SELECT status FROM suspension.suspension_request WHERE project_id = '{projectId}'"));
        JsonObject periods = await client.GetOrFailAsync(sessions.EntityManager, $"{SuspensionDriver.Suspensions}?projectId={projectId}&open=true");
        Assert.Equal(first, Guid.Parse(Assert.Single(periods["items"]!.AsArray())!.Text("suspensionRequestId")));
    }

    /// <summary>
    /// Acceptance criterion 3: WF-11's approval makes the request APPROVED and nothing more — the project stays ACTIVE, no suspension opens,
    /// no Project event is written; the run's requester is the originator, so they cannot approve it themselves; the same outcome handed to
    /// WF-09 again applies nothing. The activation is a later transaction, by WF-09's service principal, with its own events.
    /// </summary>
    [Fact]
    public async Task ApprovalAndLifecycleActivationAreTwoSeparatelyAuditedEvents()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();
        string projectBefore = await host.RowAsync("project.project", projectId);

        Guid requestId = await host.ApprovedAsync(client, sessions, projectId, "SUSPEND");

        JsonObject approved = await client.RequestAsync(sessions.EntityManager, requestId);
        Assert.Equal(("APPROVED", null, null), (approved.Text("status"), approved["effectedAt"]?.GetValue<string>(), approved["suspension"]));
        Assert.Equal(projectBefore, await host.RowAsync("project.project", projectId));
        Assert.Empty(await host.SuspensionPeriodsAsync(projectId));
        Assert.Empty(await host.AuditTrailAsync("Project", projectId));
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync(requestId));
        Assert.Equal((SuspensionDriver.Person(8), "SUSPENSION"), (run.RequestedByUserId, run.RoutingKey));

        // The outcome delivered again: WF-09's handler, handed the same payload, records it as ignored and changes nothing.
        string requestRow = await host.RowAsync("suspension.suspension_request", requestId);
        string payload = Assert.Single(await host.Database.QueryAsync($"SELECT payload::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{run.Id}-outcome'"));
        await host.WithScopeAsync(async services =>
        {
            await services.GetServices<IApprovalOutcomeHandler>().Single(h => h.SubjectModule == "Suspension")
                .HandleAsync(EventSerialization.Deserialize<ApprovalOutcomeRecorded>(payload), CancellationToken.None);
            return true;
        });
        Assert.Equal(requestRow, await host.RowAsync("suspension.suspension_request", requestId));
        string approver = SuspensionDriver.Person(2).ToString();
        Assert.Equal(
            [
                $"Suspension.RequestCreated USER {SuspensionDriver.Person(8)}", $"Suspension.RequestSubmitted USER {SuspensionDriver.Person(8)}",
                $"Suspension.ReviewStarted USER {SuspensionDriver.Person(3)}", $"Suspension.RequestApproved USER {approver}",
                $"Suspension.OutcomeIgnored USER {approver}",
            ],
            await host.AuditTrailAsync("Suspension", requestId));

        await host.ActivateDueAsync();

        JsonObject effected = await client.RequestAsync(sessions.EntityManager, requestId);
        Assert.Equal("EFFECTED", effected.Text("status"));
        Assert.NotNull(effected["effectedAt"]);
        Assert.Null(effected["suspension"]!["endedAt"]);
        Assert.Equal("SUSPENDED", await host.LifecycleOfAsync(projectId));
        string service = SuspensionServicePrincipal.Id.ToString();
        Assert.Equal($"Suspension.RequestEffected SERVICE {service}", (await host.AuditTrailAsync("Suspension", requestId))[^1]);
        Assert.Equal([$"Project.ProjectSuspended SERVICE {service}"], await host.AuditTrailAsync("Project", projectId));
        Assert.Equal(
            [requestId.ToString()],
            await host.Database.QueryAsync($"""
                SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.subject_module = 'Project' AND e.subject_id = '{projectId}' AND a.attribute_name = 'suspension_request_id'
                """));
    }

    /// <summary>
    /// Validation check 2 and acceptance criterion 2: suspension and resumption leave the ACTIVE Approved Baseline exactly as it was — the
    /// row, its row version, every baseline of the project — and write no Schedule event. While suspended, WF-03 takes no change; once
    /// resumed, a rebaseline is still WF-03's explicit action and needs WF-08's authorisation, so a candidate without one is refused.
    /// </summary>
    [Fact]
    public async Task ResumingAProjectChangesNoBaselineWithoutASeparateExplicitAction()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();
        Guid baselineId = await client.BaselinedAsync(sessions, projectId);
        string baselineBefore = await host.RowAsync("schedule.project_baseline", baselineId);
        DateTimeOffset since = await host.NowAsync();

        await host.EffectedAsync(client, sessions, projectId, "SUSPEND");
        using (HttpResponseMessage whileSuspended = await client.PostAsync(SuspensionDriver.Baselines, sessions.EntityManager, new { projectId }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_PROJECT_NOT_ELIGIBLE"), await whileSuspended.RefusalAsync());
        }

        Guid resumption = await host.ApprovedAsync(client, sessions, projectId, "RESUME");
        JsonObject resumed = await client.CommandOrFailAsync(sessions.Officer, resumption, "activate");

        Assert.Equal("EFFECTED", resumed.Text("status"));
        Assert.Equal(resumption, Guid.Parse(resumed["suspension"]!.Text("resumptionRequestId")));
        Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));
        Assert.Equal(["ended"], await host.SuspensionPeriodsAsync(projectId));
        Assert.Equal(baselineBefore, await host.RowAsync("schedule.project_baseline", baselineId));
        Assert.Equal(["1 ACTIVE"], await host.BaselineStatesAsync(projectId));
        IReadOnlyList<string> events = await host.ProjectEventsSinceAsync(projectId, since);
        Assert.DoesNotContain(events, e => e.StartsWith("Schedule ", StringComparison.Ordinal));
        Assert.Contains("Project Project.ProjectResumed", events);

        // The explicit action: a rebaseline is a WF-03 candidate that names a WF-08 change authorisation; without one it is refused.
        Guid candidate = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, SuspensionDriver.Baselines, new { projectId }));
        using HttpResponseMessage rebaseline = await client.PostAsync($"{SuspensionDriver.Baselines}/{candidate}/submit", sessions.EntityManager, new { changeAuthorizationId = (Guid?)null });
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED"), await rebaseline.RefusalAsync());
        Assert.Equal(baselineBefore, await host.RowAsync("schedule.project_baseline", baselineId));
    }

    /// <summary>
    /// WF-09 §4.1 and SUS-CC-23: an approved request takes effect on its effective date, not before — WF-09's pass leaves it APPROVED and the
    /// command refuses it 409 SUSPENSION_NOT_YET_EFFECTIVE; once the date comes, the pass activates it — and an effective date in the past
    /// is refused at submission.
    /// </summary>
    [Fact]
    public async Task ARequestTakesEffectOnItsEffectiveDateAndNotBefore()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();

        Guid backdated = await client.RaiseAsync(sessions.EntityManager, SuspensionDriver.RequestBody(projectId, "SUSPEND", SuspensionDriver.Today.AddDays(-1)));
        using (HttpResponseMessage submitted = await client.CommandAsync(sessions.EntityManager, backdated, "submit"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SUSPENSION_DATE_INVALID"), await submitted.RefusalAsync());
        }

        using (HttpResponseMessage deleted = await client.SendAsync(HttpMethod.Delete, $"{SuspensionDriver.Requests}/{backdated}", sessions.EntityManager))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        Guid requestId = await host.ApprovedAsync(client, sessions, projectId, "SUSPEND", SuspensionDriver.Today.AddDays(7));
        await host.ActivateDueAsync();
        using (HttpResponseMessage early = await client.CommandAsync(sessions.Officer, requestId, "activate"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "SUSPENSION_NOT_YET_EFFECTIVE"), await early.RefusalAsync());
        }

        Assert.Equal("APPROVED", (await client.RequestAsync(sessions.Officer, requestId)).Text("status"));
        Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));
        Assert.Empty(await host.SuspensionPeriodsAsync(projectId));

        host.Identity.Clock.Advance(TimeSpan.FromDays(7));
        try
        {
            await host.ActivateDueAsync();
        }
        finally
        {
            host.Identity.Clock.Reset();
        }

        Assert.Equal(["EFFECTED"], await host.Database.QueryAsync($"SELECT status FROM suspension.suspension_request WHERE id = '{requestId}'"));
        Assert.Equal("SUSPENDED", await host.LifecycleOfAsync(projectId));
        Assert.Equal(["open"], await host.SuspensionPeriodsAsync(projectId));
    }

    /// <summary>
    /// SUS-CC-10: activation is safe to retry. The command sent again — under its own Idempotency-Key or a new one — finds the request
    /// EFFECTED and is refused 409 TERMINAL_STATE, and WF-09's pass finds nothing due: the project moved once. (The platform keeps no
    /// replay store yet, api-conventions R-37; suspension.md F-9.)
    /// </summary>
    [Fact]
    public async Task ActivationTakesEffectOnceHoweverItIsRetried()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();
        Guid requestId = await host.ApprovedAsync(client, sessions, projectId, "SUSPEND");
        string key = Guid.NewGuid().ToString();

        using (HttpResponseMessage first = await client.CommandAsync(sessions.Officer, requestId, "activate", key))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        string projectAfter = await host.RowAsync("project.project", projectId);
        foreach (string? retryKey in new[] { key, null })
        {
            using HttpResponseMessage again = await client.CommandAsync(sessions.Officer, requestId, "activate", retryKey);
            Assert.Equal((HttpStatusCode.Conflict, "TERMINAL_STATE"), await again.RefusalAsync());
        }

        await host.ActivateDueAsync();
        Assert.Equal(projectAfter, await host.RowAsync("project.project", projectId));
        Assert.Equal(["open"], await host.SuspensionPeriodsAsync(projectId));
        Assert.Single(await host.AuditTrailAsync("Project", projectId));
    }

    /// <summary>
    /// ADR-013: the entity Project Manager raises and submits, AHDA reviews and activates. An external user is refused review and activation
    /// whatever they hold, and the refusal is audited; a resumption is raised only for a suspended project, one open at a time.
    /// </summary>
    [Fact]
    public async Task TheEntityRaisesAndAhdaDecidesAndActivates()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();

        using (HttpResponseMessage early = await client.PostAsync(SuspensionDriver.Requests, sessions.EntityManager, SuspensionDriver.RequestBody(projectId, "RESUME", SuspensionDriver.Today)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SUSPENSION_PROJECT_NOT_ELIGIBLE"), await early.RefusalAsync());
        }

        Guid requestId = await client.RaiseAsync(sessions.EntityManager, SuspensionDriver.RequestBody(projectId, "SUSPEND", SuspensionDriver.Today, SuspensionDriver.Today.AddMonths(3)));
        await client.CommandOrFailAsync(sessions.EntityManager, requestId, "submit");
        using (HttpResponseMessage review = await client.CommandAsync(sessions.EntityManager, requestId, "start-review"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
        }

        await client.CommandOrFailAsync(sessions.DepartmentManager, requestId, "start-review");
        await host.DecideAndDeliverAsync(requestId, ApprovalTaskDecision.Approve);
        await client.CommandOrFailAsync(sessions.Officer, requestId, "activate");

        Guid resumption = await client.RaiseAsync(sessions.EntityManager, SuspensionDriver.RequestBody(projectId, "RESUME", SuspensionDriver.Today));
        using (HttpResponseMessage second = await client.PostAsync(SuspensionDriver.Requests, sessions.Officer, SuspensionDriver.RequestBody(projectId, "RESUME", SuspensionDriver.Today)))
        {
            Assert.Equal((HttpStatusCode.Conflict, "SUSPENSION_RESUMPTION_ALREADY_EXISTS"), await second.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.EntityManager, resumption, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, resumption, "start-review");
        await host.DecideAndDeliverAsync(resumption, ApprovalTaskDecision.Approve);
        using (HttpResponseMessage activate = await client.CommandAsync(sessions.EntityManager, resumption, "activate"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, activate.StatusCode);
        }

        Assert.Equal("SUSPENDED", await host.LifecycleOfAsync(projectId));
        Assert.Contains($"Suspension.AuthorityRefused USER {SuspensionDriver.Person(8)}", await host.AuditTrailAsync("Suspension", requestId));
        Assert.Contains($"Suspension.AuthorityRefused USER {SuspensionDriver.Person(8)}", await host.AuditTrailAsync("Suspension", resumption));
        await host.ActivateDueAsync();
        Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));
    }

    /// <summary>
    /// WF-11's other decisions: a RETURNED request is corrected and resubmitted as the next revision, which a new run reviews; a REJECTED
    /// one is final, and the project may then be the subject of a new request (BR-SUS-025).
    /// </summary>
    [Fact]
    public async Task AReturnedRequestComesBackAsTheNextRevisionAndARejectedOneIsFinal()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, SuspensionDriver.RequestBody(projectId, "SUSPEND", SuspensionDriver.Today));
        await client.CommandOrFailAsync(sessions.EntityManager, requestId, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, requestId, "start-review");

        await host.DecideAndDeliverAsync(requestId, ApprovalTaskDecision.Return);
        Assert.Equal("RETURNED", (await client.RequestAsync(sessions.EntityManager, requestId)).Text("status"));
        JsonObject resubmitted = await client.CommandOrFailAsync(sessions.EntityManager, requestId, "submit");
        Assert.Equal(("SUBMITTED", 2), (resubmitted.Text("status"), resubmitted["revisionNo"]!.GetValue<int>()));
        await client.CommandOrFailAsync(sessions.DepartmentManager, requestId, "start-review");
        Assert.Equal([1, 2], (await host.RunsAsync(requestId)).Select(r => r.Subject.RevisionNo));

        await host.DecideAndDeliverAsync(requestId, ApprovalTaskDecision.Reject);
        Assert.Equal("REJECTED", (await client.RequestAsync(sessions.EntityManager, requestId)).Text("status"));
        using (HttpResponseMessage activate = await client.CommandAsync(sessions.Officer, requestId, "activate"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "TERMINAL_STATE"), await activate.RefusalAsync());
        }

        Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));
        await client.RaiseAsync(sessions.EntityManager, SuspensionDriver.RequestBody(projectId, "SUSPEND", SuspensionDriver.Today));
    }
}
