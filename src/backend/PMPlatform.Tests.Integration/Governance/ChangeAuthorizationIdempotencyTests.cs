using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Integration.ChangeRequest;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Governance;

/// <summary>
/// Invariant 1 (change-request.md D-6, D-7): a ChangeAuthorization is issued once per governed commitment an approved change request
/// changes, and applied by its target module exactly once — however the WF-11 outcome that issues or applies it is delivered again, and
/// however the request that applies it is retried, in sequence or racing on the network under one <c>Idempotency-Key</c>. Held for each
/// target module, WF-03 (applied as its rebaseline activates at once) and WF-14 (applied as WF-11 approves its new budget version).
/// </summary>
[Collection(ChangeRequestSuite.Name)]
public sealed class ChangeAuthorizationIdempotencyTests(ChangeRequestTestHost host)
{
    /// <summary>How many copies of one delivery or request race each time.</summary>
    private const int Copies = 4;

    /// <summary>
    /// A request changing schedule, cost and scope — every dimension a change request states, so a scope WF-08 starts to issue an
    /// authorisation for appears here — is approved with its outcome dispatched four times at once and then handed to WF-08 again: two
    /// authorisations, issued once. Each is then applied by its module's own step, sent four times at once under one key and retried: one
    /// application each, by the person and record that made the change; the typed adapter recognises the same application and refuses any
    /// other; the authorisations, the baselines and the budget versions are unchanged by every repeat, to the row version.
    /// </summary>
    [Fact]
    public async Task EachAuthorizationIsIssuedOnceAndAppliedOnceHoweverItsDeliveryOrApplicationIsRepeated()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        Guid baseline = await host.BaselinedAsync(client, sessions, projectId);
        Guid budget = await host.BudgetedAsync(client, sessions, projectId, "1000000.00");

        // Issuance: WF-11's approval delivered by four dispatchers at once, then handed to WF-08 again (EV-5).
        Guid requestId = await client.RaiseAsync(sessions.EntityManager,
            ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", costImpactSar: "30000.00", scheduleImpactDays: 4, scopeImpact: "One more loading bay."));
        await client.UnderReviewAsync(sessions, requestId);
        ApprovalInstanceDetail review = await host.DecideAsync(ChangeRequestApprovalRouting.SubjectModule, ChangeRequestApprovalRouting.SubjectType, requestId, ApprovalTaskDecision.Approve);
        Assert.Single(await DispatchAtOnceAsync(review.Id), delivered => delivered);
        await HandAgainAsync(ChangeRequestApprovalRouting.SubjectModule, review.Id);

        JsonArray issued = (await client.CommandOrFailAsync(sessions.Officer, requestId, "start-implementation"))["authorizations"]!.AsArray();
        Assert.Equal(
            [$"COMMITMENT_CHANGE {budget} 1 ISSUED", $"REBASELINE {baseline} 1 ISSUED"],
            issued.Select(a => a!).Select(a => $"{a.Text("authorizationScope")} {a.Text("targetId")} {a["targetRevisionNo"]} {a.Text("status")}").Order(StringComparer.Ordinal));
        Guid rebaseline = ChangeRequestDriver.AuthorizationOf(issued, "REBASELINE");
        Guid commitmentChange = ChangeRequestDriver.AuthorizationOf(issued, "COMMITMENT_CHANGE");
        Assert.Equal(
            ["ChangeRequest.ChangeAuthorizationIssued 2", "ChangeRequest.ChangeRequestApproved 1", "ChangeRequest.OutcomeIgnored 1"],
            await host.Database.QueryAsync($"""
                SELECT event_type || ' ' || count(*) FROM audit_activity.audit_event
                WHERE subject_id = '{requestId}' AND event_type IN ('ChangeRequest.ChangeAuthorizationIssued', 'ChangeRequest.ChangeRequestApproved', 'ChangeRequest.OutcomeIgnored')
                GROUP BY event_type ORDER BY event_type
                """));

        // WF-03: the rebaseline that applies it, on a LIGHT project, submitted four times at once under one key, then retried.
        Guid candidate = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, ChangeRequestDriver.Baselines, new { projectId }));
        string rebaselineKey = Guid.NewGuid().ToString();
        Answer[] rebaselines = await GovernanceApi.AtOnceAsync(Copies, _ => GovernanceApi.AnswerAsync(client.SendAsync(
            HttpMethod.Post, $"{ChangeRequestDriver.Baselines}/{candidate}/submit", sessions.EntityManager, new { changeAuthorizationId = rebaseline }, idempotencyKey: rebaselineKey)));
        AssertAppliedOnce(rebaselines, "the rebaseline");
        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
        string[] scheduleRows = [await host.RowAsync("schedule.project_baseline", baseline), await host.RowAsync("schedule.project_baseline", candidate),
                                 await host.RowAsync("change_request.change_authorization", rebaseline)];
        using (HttpResponseMessage retried = await client.SendAsync(
                   HttpMethod.Post, $"{ChangeRequestDriver.Baselines}/{candidate}/submit", sessions.EntityManager, new { changeAuthorizationId = rebaseline }, idempotencyKey: rebaselineKey))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await retried.RefusalAsync());
        }

        // WF-14: the new budget version that applies it submitted four times at once under one key, then retried; WF-11's approval
        // delivered by four dispatchers at once, then handed to WF-14 again.
        Guid version = await host.DraftBudgetAsync(client, sessions, projectId, "1030000.00");
        string budgetKey = Guid.NewGuid().ToString();
        Answer[] submissions = await GovernanceApi.AtOnceAsync(Copies, _ => GovernanceApi.AnswerAsync(client.SendAsync(
            HttpMethod.Post, $"{ChangeRequestDriver.Commitments}/{version}/submit", sessions.EntityManager, new { changeAuthorizationId = commitmentChange }, idempotencyKey: budgetKey)));
        AssertAppliedOnce(submissions, "the budget submission");
        using (HttpResponseMessage retried = await client.SendAsync(
                   HttpMethod.Post, $"{ChangeRequestDriver.Commitments}/{version}/submit", sessions.EntityManager, new { changeAuthorizationId = commitmentChange }, idempotencyKey: budgetKey))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await retried.RefusalAsync());
        }

        ApprovalInstanceDetail budgetReview = await host.DecideAsync("FinancialKpi", "FinancialCommitment", version, ApprovalTaskDecision.Approve);
        Assert.Single(await host.RunsAsync("FinancialKpi", "FinancialCommitment", version));
        Assert.Single(await DispatchAtOnceAsync(budgetReview.Id), delivered => delivered);
        Assert.Equal(["1 SUPERSEDED 1000000.00", "2 ACTIVE 1030000.00"], await host.BudgetStatesAsync(projectId));
        string[] budgetRows = [await host.RowAsync("financial_kpi.financial_commitment", budget), await host.RowAsync("financial_kpi.financial_commitment", version),
                               await host.RowAsync("change_request.change_authorization", commitmentChange)];
        await HandAgainAsync("FinancialKpi", budgetReview.Id);

        // The typed adapter: the same application again is recognised and writes nothing; any other is refused.
        foreach ((Guid authorization, ChangeAuthorizationScope scope, Guid target, string module, string type, Guid record) in new[]
                 {
                     (rebaseline, ChangeAuthorizationScope.Rebaseline, baseline, ChangeTargets.ScheduleModule, ChangeTargets.ProjectBaseline, candidate),
                     (commitmentChange, ChangeAuthorizationScope.CommitmentChange, budget, ChangeTargets.FinancialKpiModule, ChangeTargets.FinancialCommitment, version),
                 })
        {
            ChangeAuthorizationClaim claim = new(authorization, projectId, scope, target, 1);
            Assert.Equal(ChangeAuthorizationVerdict.Replayed, await ApplyAsync(claim, ChangeAuthorizationUse.ReferenceOf(module, type, record)));
            Assert.Equal(ChangeAuthorizationVerdict.Consumed, await ApplyAsync(claim, ChangeAuthorizationUse.ReferenceOf(module, type, Guid.NewGuid())));
        }

        // Every repeat left everything as the one application made it.
        string[] scheduleRowsAfter = [await host.RowAsync("schedule.project_baseline", baseline), await host.RowAsync("schedule.project_baseline", candidate),
                                      await host.RowAsync("change_request.change_authorization", rebaseline)];
        string[] budgetRowsAfter = [await host.RowAsync("financial_kpi.financial_commitment", budget), await host.RowAsync("financial_kpi.financial_commitment", version),
                                    await host.RowAsync("change_request.change_authorization", commitmentChange)];
        Assert.Equal(scheduleRows, scheduleRowsAfter);
        Assert.Equal(budgetRows, budgetRowsAfter);
        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
        Assert.Equal(
            [$"COMMITMENT_CHANGE APPLIED {ChangeRequestDriver.Person(2)} FinancialKpi.FinancialCommitment:{version}",
             $"REBASELINE APPLIED {ChangeRequestDriver.Person(8)} Schedule.ProjectBaseline:{candidate}"],
            await host.Database.QueryAsync($"""
                SELECT authorization_scope || ' ' || status || ' ' || applied_by_user_id || ' ' || applied_reference FROM change_request.change_authorization
                WHERE change_request_id = '{requestId}' ORDER BY authorization_scope
                """));
        Assert.Equal(
            [rebaseline.ToString(), commitmentChange.ToString()],
            await host.Database.QueryAsync($"""
                SELECT change_authorization_id::text FROM schedule.project_baseline WHERE id = '{candidate}'
                UNION ALL SELECT change_authorization_id::text FROM financial_kpi.financial_commitment WHERE id = '{version}'
                """));
        Assert.Equal(2, (await host.AuditEventsAsync("ChangeRequest", requestId)).Count(e => e == "ChangeRequest.ChangeAuthorizationApplied"));
        Assert.Equal("IMPLEMENTED", (await client.CommandOrFailAsync(sessions.Officer, requestId, "mark-implemented")).Text("status"));
    }

    /// <summary>Exactly one copy succeeded; every other was refused as a conflict, none failed as an error.</summary>
    private static void AssertAppliedOnce(Answer[] answers, string what)
    {
        Assert.True(answers.Count(a => a.Succeeded) == 1 && answers.All(a => a.Succeeded || a.Status is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed),
            $"{what}, sent {answers.Length} times at once under one key, answered: {string.Join(", ", answers.Select(a => a.ToString()))}");
    }

    /// <summary>The run's outcome dispatched by <see cref="Copies"/> dispatchers at once, as several API instances' outbox workers would.</summary>
    private async Task<bool[]> DispatchAtOnceAsync(Guid runId)
    {
        Guid message = await host.OutcomeMessageAsync(runId);
        IOutboxDispatcher dispatcher = host.Api.Services.GetRequiredService<IOutboxDispatcher>();
        return await GovernanceApi.AtOnceAsync(Copies, _ => dispatcher.DispatchAsync(message, CancellationToken.None));
    }

    /// <summary>
    /// The run's outcome handed to its subject module's handler once more, as a redelivery after the dispatch mark was lost would: the
    /// dispatcher no longer offers it, so this is the handler's own idempotency (EV-5).
    /// </summary>
    private async Task HandAgainAsync(string subjectModule, Guid runId)
    {
        string payload = Assert.Single(await host.Database.QueryAsync($"SELECT payload::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{runId}-outcome'"));
        await host.WithScopeAsync(async services =>
        {
            await services.GetServices<IApprovalOutcomeHandler>().Single(h => h.SubjectModule == subjectModule)
                .HandleAsync(EventSerialization.Deserialize<ApprovalOutcomeRecorded>(payload), CancellationToken.None);
            return true;
        });
    }

    /// <summary>The typed adapter's application by the Project Manager, in a unit of work of its own that commits what it staged.</summary>
    private Task<ChangeAuthorizationVerdict> ApplyAsync(ChangeAuthorizationClaim claim, string reference) =>
        host.WithScopeAsync(async services =>
        {
            ChangeAuthorizationVerdict verdict = await services.GetRequiredService<IChangeAuthorizations>()
                .ApplyAsync(claim, new ChangeAuthorizationUse(ChangeRequestDriver.Person(8), reference, DateTimeOffset.UtcNow), CancellationToken.None);
            await services.GetRequiredService<PMPlatformDbContext>().SaveChangesAsync();
            return verdict;
        });
}
