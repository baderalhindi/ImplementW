using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Approval;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.Approval.ApprovalDriver;

namespace PMPlatform.Tests.Integration.Approval;

/// <summary>
/// TASK-035 acceptance criterion 2 and its validation check: a delegated approver never gains authority the delegator
/// did not have — along a chain, after a revocation, after the period — and ADR-013: no external approval authority of
/// any kind, in the application and in the database. Each test gives its own delegations and revokes them after.
/// </summary>
[Collection(ApprovalSuite.Name)]
public sealed class DelegationTests(ApprovalTestHost host) : IDisposable
{
    public void Dispose() => host.Clock.Reset();

    [Fact]
    public async Task ADelegateDecidesUnderTheDelegatorsAuthorityAndTheTaskRecordsBoth()
    {
        ApprovalDelegationDetail delegation = (await host.DelegateAsync(2, 5)).Value!;
        ApprovalInstanceDetail run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.SingleStage);

        ApprovalTaskDetail decided = (await host.ApproveAsync(5, run.Tasks.Single().Id)).Tasks.Single();

        Assert.Equal((Person(2), Person(5), delegation.Id), (decided.AssignedUserId!.Value, decided.ActingUserId!.Value, decided.ApprovalDelegationId!.Value));
        await RevokeAsync(2, delegation.Id);
    }

    /// <summary>
    /// The validation check: a chain 2 → 5 → 6. local.r05 decides an R02 task under 2's authority; local.r06, one link
    /// further, gains nothing, because 5 holds no authority of their own. Neither delegate reaches an R03 task, which 2
    /// could not decide either. Each refusal is audited; a run the caller may not even see is answered as not found (R-47).
    /// </summary>
    [Fact]
    public async Task ADelegationChainNeverExceedsTheFirstDelegatorsAuthority()
    {
        ApprovalDelegationDetail first = (await host.DelegateAsync(2, 5)).Value!;
        ApprovalDelegationDetail second = (await host.DelegateAsync(5, 6)).Value!;
        ApprovalInstanceDetail r02Run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.SingleStage, requester: 1);
        ApprovalInstanceDetail r03Run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.TwoStages, requester: 1);
        Guid r03Task = r03Run.Tasks.Single(t => t.SequenceNo == 1).Id;

        Assert.Equal(AdministrationErrorKind.NotFound, (await host.DecideAsync(6, r02Run.Tasks.Single().Id, ApprovalTaskDecision.Approve)).Error!.Kind);
        Assert.Equal(AdministrationErrorKind.NotFound, (await host.DecideAsync(5, r03Task, ApprovalTaskDecision.Approve)).Error!.Kind);
        Assert.Equal(AdministrationErrorKind.NotFound, (await host.DecideAsync(6, r03Task, ApprovalTaskDecision.Approve)).Error!.Kind);
        Assert.Contains(
            "NO_AUTHORITY",
            await host.Database.QueryAsync($"""
                SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.event_type = 'Approval.DecisionRefused' AND e.actor_user_id = '{Person(6)}' AND a.attribute_name = 'refusal_reason'
                """));

        Assert.Equal(Person(2), (await host.ApproveAsync(5, r02Run.Tasks.Single().Id)).Tasks.Single().AssignedUserId);
        await RevokeAsync(5, second.Id);
        await RevokeAsync(2, first.Id);
    }

    /// <summary>Decision-time revalidation: a delegation revoked, or past its period, conveys nothing from then on.</summary>
    [Fact]
    public async Task ARevokedOrLapsedDelegationConveysNothing()
    {
        ApprovalInstanceDetail run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.SingleStage);
        Guid task = run.Tasks.Single().Id;

        ApprovalDelegationDetail revoked = (await host.DelegateAsync(2, 5)).Value!;
        await RevokeAsync(2, revoked.Id);
        Assert.Equal(AdministrationErrorKind.NotFound, (await host.DecideAsync(5, task, ApprovalTaskDecision.Approve)).Error!.Kind);

        ApprovalDelegationDetail lapsing = (await host.DelegateAsync(2, 5, period: TimeSpan.FromHours(1))).Value!;
        host.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(AdministrationErrorKind.NotFound, (await host.DecideAsync(5, task, ApprovalTaskDecision.Approve)).Error!.Kind);

        await host.RunMaintenanceAsync();
        Assert.Equal(["EXPIRED"], await host.Database.QueryAsync($"SELECT status FROM approval.approval_delegation WHERE id = '{lapsing.Id}'"));
    }

    /// <summary>ADR-013: an external user is refused as a delegate, cannot decide though they hold the stage's role and APPROVAL_DECIDE, and the database refuses them too.</summary>
    [Fact]
    public async Task NoExternalUserHoldsApprovalAuthorityOfAnyKind()
    {
        AdministrationResult<ApprovalDelegationDetail> toExternal = await host.DelegateAsync(2, 8);
        Assert.Equal(ApprovalErrorCodes.DelegateInvalid, toExternal.Error!.Code);
        Assert.Equal(AdministrationErrorKind.Forbidden, (await host.DelegateAsync(8, 2)).Error!.Kind);

        // local.r08 holds R04 with APPROVAL_DECIDE on the entity project the run is for.
        ApprovalInstanceDetail run = await host.StartAsync(
            await host.NewSubjectAsync(), 1, ApprovalTestHost.ProjectManagerStage, projectId: Guid.Parse(IdentityDatabase.EntityProjectId));
        Assert.Equal(AdministrationErrorKind.NotFound, (await host.DecideAsync(8, run.Tasks.Single().Id, ApprovalTaskDecision.Approve)).Error!.Kind);
        Assert.Contains(
            "EXTERNAL_USER",
            await host.Database.QueryAsync($"""
                SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.event_type = 'Approval.DecisionRefused' AND e.actor_user_id = '{Person(8)}' AND a.attribute_name = 'refusal_reason'
                """));

        foreach (string statement in new[]
                 {
                     $"UPDATE approval.approval_task SET acting_user_id = '{Person(8)}' WHERE id = '{run.Tasks.Single().Id}'",
                     $"UPDATE approval.approval_task SET assigned_user_id = '{Person(8)}' WHERE id = '{run.Tasks.Single().Id}'",
                     $"""
                     INSERT INTO approval.approval_delegation (id, delegator_user_id, delegate_user_id, valid_from, valid_to, status, created_at, created_by, updated_at, updated_by)
                     VALUES (gen_random_uuid(), '{Person(2)}', '{Person(8)}', now(), now() + interval '1 day', 'ACTIVE', now(), '{Person(2)}', now(), '{Person(2)}')
                     """,
                 })
        {
            PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(statement));
            Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_approval_internal_authority"), (refused.SqlState, refused.ConstraintName));
        }
    }

    [Fact]
    public async Task ADelegationNeedsAnotherPersonAndAForwardPeriod()
    {
        Assert.Equal(ApprovalErrorCodes.DelegateInvalid, (await host.DelegateAsync(2, 2)).Error!.Code);
        Assert.Equal(ApprovalErrorCodes.DelegateInvalid, (await host.DelegateAsync(2, 4)).Error!.Code);
        Assert.Equal(ApprovalErrorCodes.DelegationPeriodInvalid, (await host.DelegateAsync(2, 5, period: TimeSpan.FromHours(-1))).Error!.Code);
    }

    private async Task RevokeAsync(int delegator, Guid delegationId)
    {
        AdministrationResult<ApprovalDelegationDetail> revoked = await host.WithScopeAsync(services =>
            services.GetRequiredService<IApprovalDelegationService>().RevokeAsync(Person(delegator), delegationId, CancellationToken.None));
        Assert.Equal(ApprovalDelegationStatus.Revoked, revoked.Value!.Status);
    }
}
