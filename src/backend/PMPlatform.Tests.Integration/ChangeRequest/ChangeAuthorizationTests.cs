using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ChangeRequest;

/// <summary>
/// TASK-060's acceptance criteria through the real API, WF-11 and the outbox: a change authorisation is applied by its target module
/// exactly once, however the request that applies it is retried; and the approval of a change request changes neither the schedule nor
/// the budget — the target module's explicit application does, audited apart from the approval.
/// </summary>
[Collection(ChangeRequestSuite.Name)]
public sealed class ChangeAuthorizationTests(ChangeRequestTestHost host)
{
    /// <summary>
    /// The workbook's first check: the rebaseline that applies an authorisation sent twice with one <c>Idempotency-Key</c> changes the
    /// schedule once — the retry finds the candidate ACTIVE and is refused, nothing moves — and the authorisation is applied once, by
    /// the rebaseline's own record. Another rebaseline presenting it is refused. The same application retried through the typed adapter
    /// is recognised and changes nothing; any other use is refused.
    /// </summary>
    [Fact]
    public async Task AnAuthorizationIsAppliedExactlyOnceEvenWhenItsApplicationIsRetried()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        Guid first = await host.BaselinedAsync(client, sessions, projectId);
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 5));
        Guid authorization = ChangeRequestDriver.AuthorizationOf(await host.ImplementingAsync(client, sessions, requestId), "REBASELINE");

        // The application: a rebaseline that activates at once on a LIGHT project, sent twice with the same key and body.
        string key = Guid.NewGuid().ToString();
        Guid candidate = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, ChangeRequestDriver.Baselines, new { projectId }));
        string submit = $"{ChangeRequestDriver.Baselines}/{candidate}/submit";
        using (HttpResponseMessage applied = await client.SendAsync(HttpMethod.Post, submit, sessions.EntityManager, new { changeAuthorizationId = authorization }, idempotencyKey: key))
        {
            Assert.Equal((HttpStatusCode.OK, "ACTIVE"), (applied.StatusCode, (await applied.ReadObjectAsync()).Text("status")));
        }

        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
        string[] rows = [await host.RowAsync("schedule.project_baseline", first), await host.RowAsync("schedule.project_baseline", candidate),
                         await host.RowAsync("change_request.change_authorization", authorization)];

        using (HttpResponseMessage retried = await client.SendAsync(HttpMethod.Post, submit, sessions.EntityManager, new { changeAuthorizationId = authorization }, idempotencyKey: key))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await retried.RefusalAsync());
        }

        using (HttpResponseMessage reused = await client.SubmitBaselineAsync(sessions.EntityManager, projectId, authorization))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED"), await reused.RefusalAsync());
        }

        // The same application again through the typed adapter is recognised; any other is refused; neither writes anything.
        ChangeAuthorizationClaim claim = new(authorization, projectId, ChangeAuthorizationScope.Rebaseline, first, 1);
        string reference = $"Schedule.ProjectBaseline:{candidate}";
        Assert.Equal(ChangeAuthorizationVerdict.Replayed, await ApplyAsync(claim, new ChangeAuthorizationUse(ChangeRequestDriver.Person(8), reference, DateTimeOffset.UtcNow)));
        Assert.Equal(ChangeAuthorizationVerdict.Consumed, await ApplyAsync(claim, new ChangeAuthorizationUse(ChangeRequestDriver.Person(8), $"Schedule.ProjectBaseline:{Guid.NewGuid()}", DateTimeOffset.UtcNow)));

        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE", "3 DRAFT"], await host.BaselineStatesAsync(projectId));
        string[] after = [await host.RowAsync("schedule.project_baseline", first), await host.RowAsync("schedule.project_baseline", candidate),
                          await host.RowAsync("change_request.change_authorization", authorization)];
        Assert.Equal(rows, after);
        Assert.Equal(
            [$"APPLIED|{ChangeRequestDriver.Person(8)}|{reference}"],
            await host.Database.QueryAsync($"SELECT status || '|' || applied_by_user_id || '|' || applied_reference FROM change_request.change_authorization WHERE id = '{authorization}'"));
        Assert.Equal([authorization.ToString()], await host.Database.QueryAsync($"SELECT change_authorization_id::text FROM schedule.project_baseline WHERE id = '{candidate}'"));
        Assert.Single(await host.AuditEventsAsync("ChangeRequest", requestId), e => e == "ChangeRequest.ChangeAuthorizationApplied");

        // Every authorisation applied: the change is implemented, then closed.
        Assert.Equal("IMPLEMENTED", (await client.CommandOrFailAsync(sessions.Officer, requestId, "mark-implemented")).Text("status"));
        Assert.Equal("CLOSED", (await client.CommandOrFailAsync(sessions.Officer, requestId, "close")).Text("status"));
    }

    /// <summary>
    /// The workbook's second check: approving a change request alters neither WF-03's nor WF-14's data — the baseline and budget rows are
    /// unchanged to the row version, and neither module records anything — and the authorisations it issued do not apply until AHDA opens
    /// the implementation. The change happens in each target module's own explicit step, each audited as an application by the person
    /// who made it, apart from the approval.
    /// </summary>
    [Fact]
    public async Task ApprovalAloneChangesNeitherTheScheduleNorTheBudget()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        Guid baseline = await host.BaselinedAsync(client, sessions, projectId);
        Guid budget = await host.BudgetedAsync(client, sessions, projectId, "1000000.00");
        string[] before = [await host.RowAsync("schedule.project_baseline", baseline), await host.RowAsync("financial_kpi.financial_commitment", budget)];
        DateTimeOffset since = await host.NowAsync();

        Guid requestId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", costImpactSar: "40000.00", scheduleImpactDays: 6));
        JsonObject approved = await host.ApprovedAsync(client, sessions, requestId);

        Assert.Equal("APPROVED", approved.Text("status"));
        JsonArray issued = approved["authorizations"]!.AsArray();
        Assert.Equal(
            [$"COMMITMENT_CHANGE FinancialKpi FinancialCommitment {budget} 1 ISSUED", $"REBASELINE Schedule ProjectBaseline {baseline} 1 ISSUED"],
            issued.Select(a => $"{a!["authorizationScope"]} {a.Text("targetModule")} {a.Text("targetType")} {a.Text("targetId")} {a["targetRevisionNo"]} {a.Text("status")}"));
        Guid rebaseline = ChangeRequestDriver.AuthorizationOf(issued, "REBASELINE");
        Guid commitmentChange = ChangeRequestDriver.AuthorizationOf(issued, "COMMITMENT_CHANGE");

        // Approved is not implementing: the authorisations do not apply yet. Opening the implementation moves nothing in either module.
        Assert.Equal(ChangeAuthorizationVerdict.NotImplementing, await CheckAsync(new ChangeAuthorizationClaim(rebaseline, projectId, ChangeAuthorizationScope.Rebaseline, baseline, 1)));
        await client.CommandOrFailAsync(sessions.Officer, requestId, "start-implementation");
        string[] after = [await host.RowAsync("schedule.project_baseline", baseline), await host.RowAsync("financial_kpi.financial_commitment", budget)];
        Assert.Equal(before, after);
        Assert.Equal(["1 ACTIVE"], await host.BaselineStatesAsync(projectId));
        Assert.Equal(["1 ACTIVE 1000000.00"], await host.BudgetStatesAsync(projectId));
        Assert.All(await host.ProjectEventsSinceAsync(projectId, since), e => Assert.Matches("^(ChangeRequest|Approval) ", e));
        Assert.Equal(["ISSUED", "ISSUED"], await host.Database.QueryAsync($"SELECT status FROM change_request.change_authorization WHERE change_request_id = '{requestId}'"));

        // The explicit application steps: the Project Manager rebaselines on WF-03, AHDA approves the budget change on WF-14.
        JsonObject activated = await client.SubmittedBaselineAsync(sessions.EntityManager, projectId, rebaseline);
        Guid newBudget = await host.BudgetedAsync(client, sessions, projectId, "1040000.00", commitmentChange);

        Assert.Equal(("ACTIVE", rebaseline.ToString()), (activated.Text("status"), activated.Text("changeAuthorizationId")));
        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
        Assert.Equal(["1 SUPERSEDED 1000000.00", "2 ACTIVE 1040000.00"], await host.BudgetStatesAsync(projectId));
        Assert.Equal(
            [$"REBASELINE|{ChangeRequestDriver.Person(8)}|Schedule.ProjectBaseline:{AdministrationApi.IdOf(activated)}",
             $"COMMITMENT_CHANGE|{ChangeRequestDriver.Person(2)}|FinancialKpi.FinancialCommitment:{newBudget}"],
            await host.Database.QueryAsync($"""
                SELECT authorization_scope || '|' || applied_by_user_id || '|' || applied_reference FROM change_request.change_authorization
                WHERE change_request_id = '{requestId}' ORDER BY applied_at
                """));

        // The request's history: approval and each application are separate events, by the people who did each.
        Assert.Equal(
            ["ChangeRequest.ChangeRequestCreated", "ChangeRequest.ChangeRequestSubmitted", "ChangeRequest.MaterialityEvaluated", "ChangeRequest.ReviewStarted",
             "ChangeRequest.ChangeRequestApproved", "ChangeRequest.ChangeAuthorizationIssued", "ChangeRequest.ChangeAuthorizationIssued",
             "ChangeRequest.ImplementationStarted", "ChangeRequest.ChangeAuthorizationApplied", "ChangeRequest.ChangeAuthorizationApplied"],
            await host.AuditEventsAsync("ChangeRequest", requestId));
        Assert.Equal(
            [$"ChangeRequest.ChangeRequestApproved {ChangeRequestDriver.Person(2)}", $"ChangeRequest.ChangeAuthorizationApplied {ChangeRequestDriver.Person(8)}",
             $"ChangeRequest.ChangeAuthorizationApplied {ChangeRequestDriver.Person(2)}"],
            await host.Database.QueryAsync($"""
                SELECT event_type || ' ' || actor_user_id FROM audit_activity.audit_event
                WHERE subject_id = '{requestId}' AND event_type IN ('ChangeRequest.ChangeRequestApproved', 'ChangeRequest.ChangeAuthorizationApplied') ORDER BY occurred_at, id
                """));
    }

    /// <summary>
    /// A rebaseline that needs WF-03's own approval is only checked when it is submitted: its authorisation applies as WF-11 activates
    /// it, with the approver as the actor, and the outcome delivered again — by the dispatcher, or straight to WF-03's handler — applies
    /// nothing more.
    /// </summary>
    [Fact]
    public async Task ARebaselineUnderApprovalAppliesItsAuthorizationOnlyAsItActivates()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.BaselinedAsync(client, sessions, projectId);
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 4));
        Guid authorization = ChangeRequestDriver.AuthorizationOf(await host.ImplementingAsync(client, sessions, requestId), "REBASELINE");

        Guid candidate = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.EntityManager, projectId, authorization));
        Assert.Equal(["1 ACTIVE", "2 SUBMITTED"], await host.BaselineStatesAsync(projectId));
        Assert.Equal(["ISSUED"], await host.Database.QueryAsync($"SELECT status FROM change_request.change_authorization WHERE id = '{authorization}'"));

        await host.DecideAndDeliverAsync("Schedule", "ProjectBaseline", candidate, ApprovalTaskDecision.Approve);
        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
        string row = await host.RowAsync("change_request.change_authorization", authorization);
        Assert.Contains($"\"applied_by_user_id\": \"{ChangeRequestDriver.Person(2)}\"", row, StringComparison.Ordinal);

        // The outcome delivered again: the dispatcher finds it dispatched, and WF-03's handler, handed the same payload, applies nothing.
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync("Schedule", "ProjectBaseline", candidate));
        Assert.False(await host.DeliverAsync(run.Id));
        string payload = Assert.Single(await host.Database.QueryAsync($"SELECT payload::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{run.Id}-outcome'"));
        await host.WithScopeAsync(async services =>
        {
            await services.GetServices<IApprovalOutcomeHandler>().Single(h => h.SubjectModule == "Schedule")
                .HandleAsync(EventSerialization.Deserialize<ApprovalOutcomeRecorded>(payload), CancellationToken.None);
            return true;
        });
        Assert.Equal(row, await host.RowAsync("change_request.change_authorization", authorization));
        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
        Assert.Single(await host.AuditEventsAsync("ChangeRequest", requestId), e => e == "ChangeRequest.ChangeAuthorizationApplied");
    }

    /// <summary>
    /// An authorisation applies to its own project, kind of change and pinned target version, while its change is being implemented
    /// (WF-08 VAL-CHG-015, BR-CHG-021): one of another project, a commitment change presented as a rebaseline, a rebaseline presented as
    /// a budget change, one only approved, and one whose baseline has moved since approval are each refused, by WF-03 and WF-14 alike.
    /// </summary>
    [Fact]
    public async Task AnAuthorizationIsRefusedOutsideItsScope()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid baseline = await host.BaselinedAsync(client, sessions, projectId);
        await host.BudgetedAsync(client, sessions, projectId, "500000.00");
        Guid otherProject = await host.ProjectAsync("LIGHT");
        await host.BaselinedAsync(client, sessions, otherProject);

        JsonArray both = await host.ImplementingAsync(client, sessions,
            await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", costImpactSar: "10000.00", scheduleImpactDays: 3)));
        Guid rebaseline = ChangeRequestDriver.AuthorizationOf(both, "REBASELINE");
        Guid commitmentChange = ChangeRequestDriver.AuthorizationOf(both, "COMMITMENT_CHANGE");
        JsonObject approvedOnly = await host.ApprovedAsync(client, sessions,
            await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 2)));
        Guid notImplementing = ChangeRequestDriver.AuthorizationOf(approvedOnly["authorizations"]!.AsArray(), "REBASELINE");
        Guid stale = ChangeRequestDriver.AuthorizationOf(await host.ImplementingAsync(client, sessions,
            await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 1))), "REBASELINE");

        Assert.Equal(ChangeAuthorizationVerdict.OutOfScope, await CheckAsync(new ChangeAuthorizationClaim(rebaseline, otherProject, ChangeAuthorizationScope.Rebaseline, baseline, 1)));
        Assert.Equal(ChangeAuthorizationVerdict.OutOfScope, await CheckAsync(new ChangeAuthorizationClaim(commitmentChange, projectId, ChangeAuthorizationScope.Rebaseline, baseline, 1)));
        Assert.Equal(ChangeAuthorizationVerdict.TargetMoved, await CheckAsync(new ChangeAuthorizationClaim(rebaseline, projectId, ChangeAuthorizationScope.Rebaseline, baseline, 2)));
        Assert.Equal(ChangeAuthorizationVerdict.NotImplementing, await CheckAsync(new ChangeAuthorizationClaim(notImplementing, projectId, ChangeAuthorizationScope.Rebaseline, baseline, 1)));
        Assert.Equal(ChangeAuthorizationVerdict.NotFound, await CheckAsync(new ChangeAuthorizationClaim(Guid.NewGuid(), projectId, ChangeAuthorizationScope.Rebaseline, baseline, 1)));

        Guid otherCandidate = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, ChangeRequestDriver.Baselines, new { projectId = otherProject }));
        Guid candidate = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, ChangeRequestDriver.Baselines, new { projectId }));
        foreach ((Guid presentedTo, Guid presented) in new[] { (otherCandidate, rebaseline), (candidate, commitmentChange), (candidate, notImplementing) })
        {
            using HttpResponseMessage refused = await client.PostAsync($"{ChangeRequestDriver.Baselines}/{presentedTo}/submit", sessions.EntityManager, new { changeAuthorizationId = presented });
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED"), await refused.RefusalAsync());
        }

        Guid budgetChange = await host.DraftBudgetAsync(client, sessions, projectId, "600000.00");
        using (HttpResponseMessage asBudget = await client.PostAsync($"{ChangeRequestDriver.Commitments}/{budgetChange}/submit", sessions.EntityManager, new { changeAuthorizationId = rebaseline }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "FINANCIAL_CHANGE_AUTHORIZATION_REQUIRED"), await asBudget.RefusalAsync());
        }

        // Two approved changes pinned to the same baseline: the first applied moves it, and the other no longer applies.
        await client.OkOrFailAsync(sessions.EntityManager, $"{ChangeRequestDriver.Baselines}/{candidate}/submit", new { changeAuthorizationId = rebaseline });
        await host.DecideAndDeliverAsync("Schedule", "ProjectBaseline", candidate, ApprovalTaskDecision.Approve);
        Assert.Equal(ChangeAuthorizationVerdict.TargetMoved, await CheckAsync(new ChangeAuthorizationClaim(stale, projectId, ChangeAuthorizationScope.Rebaseline, candidate, 2)));
        using (HttpResponseMessage moved = await client.SubmitBaselineAsync(sessions.EntityManager, projectId, stale))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED"), await moved.RefusalAsync());
        }

        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE", "3 DRAFT"], await host.BaselineStatesAsync(projectId));
        Assert.Equal(["1 ACTIVE", "2 DRAFT"], await host.BaselineStatesAsync(otherProject));
        Assert.Equal(["1 ACTIVE 500000.00", "2 DRAFT 600000.00"], await host.BudgetStatesAsync(projectId));
        Assert.Equal(
            new[] { $"{rebaseline} APPLIED", $"{commitmentChange} ISSUED", $"{notImplementing} ISSUED", $"{stale} ISSUED" }.Order(StringComparer.Ordinal),
            (await host.Database.QueryAsync($"""
                SELECT id || ' ' || status FROM change_request.change_authorization
                WHERE id IN ('{rebaseline}', '{commitmentChange}', '{notImplementing}', '{stale}')
                """)).Order(StringComparer.Ordinal));
    }

    private Task<ChangeAuthorizationVerdict> CheckAsync(ChangeAuthorizationClaim claim) =>
        host.WithScopeAsync(services => services.GetRequiredService<IChangeAuthorizations>().CheckAsync(claim, CancellationToken.None));

    /// <summary>The typed adapter's application, in a unit of work of its own that commits what it staged, as a target module's save does.</summary>
    private Task<ChangeAuthorizationVerdict> ApplyAsync(ChangeAuthorizationClaim claim, ChangeAuthorizationUse use) =>
        host.WithScopeAsync(async services =>
        {
            ChangeAuthorizationVerdict verdict = await services.GetRequiredService<IChangeAuthorizations>().ApplyAsync(claim, use, CancellationToken.None);
            await services.GetRequiredService<Infrastructure.Persistence.PMPlatformDbContext>().SaveChangesAsync();
            return verdict;
        });
}
