using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;
using static PMPlatform.Tests.Integration.Approval.ApprovalDriver;

namespace PMPlatform.Tests.Integration.Approval;

/// <summary>
/// TASK-035 routing and escalation mechanics: stages in order, each started when reached; an overdue task replaced by one
/// for WORKFLOW_POLICY's escalation role; the escalated task kept, linked, in the history.
/// </summary>
[Collection(ApprovalSuite.Name)]
public sealed class EscalationTests(ApprovalTestHost host) : IDisposable
{
    public void Dispose() => host.Clock.Reset();

    [Fact]
    public async Task StagesRunInOrderAndTheLastApprovalEndsTheRun()
    {
        ApprovalInstanceDetail run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.TwoStages);
        ApprovalTaskDetail first = run.Tasks.Single(t => t.SequenceNo == 1);
        ApprovalTaskDetail second = run.Tasks.Single(t => t.SequenceNo == 2);
        Assert.Equal((true, false), (first.DueAt.HasValue, second.DueAt.HasValue));

        Assert.Equal(AdministrationErrorKind.InvalidTransition, (await host.DecideAsync(2, second.Id, ApprovalTaskDecision.Approve)).Error!.Kind);

        ApprovalInstanceDetail afterFirst = await host.ApproveAsync(3, first.Id);
        Assert.Equal(ApprovalInstanceStatus.Pending, afterFirst.Status);
        Assert.NotNull(afterFirst.Tasks.Single(t => t.SequenceNo == 2).DueAt);

        Assert.Equal(ApprovalInstanceStatus.Approved, (await host.ApproveAsync(2, second.Id)).Status);
    }

    [Fact]
    public async Task AnOverdueTaskIsEscalatedToTheEscalationRoleAndItsReplacementDecides()
    {
        ApprovalInstanceDetail run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.TwoStages);
        Guid overdue = run.Tasks.Single(t => t.SequenceNo == 1).Id;
        host.Clock.Advance(TimeSpan.FromDays(ApprovalTestHost.DueDays + 1));

        Assert.True(await host.RunMaintenanceAsync() >= 1);

        ApprovalInstanceDetail escalated = (await host.WithScopeAsync(services =>
            services.GetRequiredService<IApprovalWorkflowService>().GetInstanceAsync(Person(6), run.Id, CancellationToken.None))).Value!;
        ApprovalTaskDetail original = escalated.Tasks.Single(t => t.Id == overdue);
        ApprovalTaskDetail replacement = escalated.Tasks.Single(t => t.Id == original.EscalatedToTaskId);
        Assert.Equal(ApprovalTaskStatus.Escalated, original.Status);
        Assert.Equal((1, Guid.Parse(ApprovalTestHost.RoleId(2)), ApprovalTaskStatus.Pending), (replacement.SequenceNo, replacement.AssignedRoleId, replacement.Status));
        Assert.Equal(host.Clock.GetUtcNow().AddDays(ApprovalTestHost.DueDays).Date, replacement.DueAt!.Value.UtcDateTime.Date);

        // The original role's holder no longer decides the stage; the escalation role's does.
        Assert.Equal(AdministrationErrorKind.TerminalState, (await host.DecideAsync(3, overdue, ApprovalTaskDecision.Approve)).Error!.Kind);
        Assert.Equal(ApprovalInstanceStatus.Pending, (await host.ApproveAsync(2, replacement.Id)).Status);
    }

    /// <summary>MOD-045: only the requester escalates, and only a task that is overdue.</summary>
    [Fact]
    public async Task OnlyTheRequesterEscalatesAndOnlyWhenOverdue()
    {
        ApprovalInstanceDetail run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.TwoStages);
        Guid task = run.Tasks.Single(t => t.SequenceNo == 1).Id;

        Assert.Equal(ApprovalErrorCodes.EscalationNotAllowed, (await EscalateAsync(6, task)).Error!.Code);
        host.Clock.Advance(TimeSpan.FromDays(ApprovalTestHost.DueDays + 1));
        Assert.Equal(AdministrationErrorKind.Forbidden, (await EscalateAsync(2, task)).Error!.Kind);

        ApprovalInstanceDetail escalated = (await EscalateAsync(6, task)).Value!;
        Assert.Equal(ApprovalTaskStatus.Escalated, escalated.Tasks.Single(t => t.Id == task).Status);
        Assert.Equal("Waiting too long.", escalated.Tasks.Single(t => t.Id == task).DecisionReason!.Text);
    }

    [Fact]
    public async Task TheRequesterWithdrawsAPendingRunAndItsOutcomeIsWithdrawn()
    {
        Guid subject = await host.NewSubjectAsync();
        ApprovalInstanceDetail run = await host.StartAsync(subject, 1, ApprovalTestHost.TwoStages);

        AdministrationResult<ApprovalInstanceDetail> byOther = await WithdrawAsync(2, run.Id);
        AdministrationResult<ApprovalInstanceDetail> withdrawn = await WithdrawAsync(6, run.Id);

        Assert.Equal(AdministrationErrorKind.Forbidden, byOther.Error!.Kind);
        Assert.Equal(ApprovalInstanceStatus.Withdrawn, withdrawn.Value!.Status);
        Assert.All(withdrawn.Value.Tasks, t => Assert.Equal(ApprovalTaskStatus.Cancelled, t.Status));
        Assert.True(await host.DispatchAsync(await host.OutcomeMessageIdAsync(run.Id)));
        Assert.Equal("WITHDRAWN|1", await host.SourceStateAsync(subject));
    }

    private Task<AdministrationResult<ApprovalInstanceDetail>> EscalateAsync(int person, Guid taskId) =>
        host.WithScopeAsync(services =>
            services.GetRequiredService<IApprovalWorkflowService>()
                .EscalateAsync(Person(person), taskId, new NarrativeText("Waiting too long.", Language.En), CancellationToken.None));

    private Task<AdministrationResult<ApprovalInstanceDetail>> WithdrawAsync(int person, Guid instanceId) =>
        host.WithScopeAsync(services =>
            services.GetRequiredService<IApprovalWorkflowService>()
                .WithdrawAsync(Person(person), instanceId, CancellationToken.None));
}
