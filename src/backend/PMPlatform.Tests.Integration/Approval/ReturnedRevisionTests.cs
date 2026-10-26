using Npgsql;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Approval;

namespace PMPlatform.Tests.Integration.Approval;

/// <summary>
/// TASK-035 acceptance criterion 3: a returned request creates a new business revision and a new linked approval run,
/// and the returned run is never mutated — not by the application, and not past it, in the database.
/// </summary>
[Collection(ApprovalSuite.Name)]
public sealed class ReturnedRevisionTests(ApprovalTestHost host)
{
    [Fact]
    public async Task AReturnedRequestComesBackAsANewRevisionWithANewLinkedRun()
    {
        Guid subject = await host.NewSubjectAsync();
        ApprovalInstanceDetail first = await host.StartAsync(subject, 1, ApprovalTestHost.SingleStage);
        AdministrationResult<ApprovalInstanceDetail> returned = await host.DecideAsync(2, first.Tasks.Single().Id, ApprovalTaskDecision.Return, "Budget lines are missing.");
        Assert.Equal(ApprovalInstanceStatus.Returned, returned.Value!.Status);
        Assert.True(await host.DispatchAsync(await host.OutcomeMessageIdAsync(first.Id)));
        Assert.Equal("RETURNED|1", await host.SourceStateAsync(subject));
        IReadOnlyList<string> before = await host.SnapshotAsync(first.Id);

        // The source revises its record: revision 2. Revision 1 cannot be approved again.
        await host.Database.ExecuteAsync($"UPDATE test_source.approvable SET revision_no = 2, status = 'SUBMITTED' WHERE id = '{subject}'");
        AdministrationResult<ApprovalInstanceDetail> stale = await host.TryStartAsync(subject, 1, ApprovalTestHost.SingleStage);
        Assert.Equal(ApprovalErrorCodes.RevisionStale, stale.Error!.Code);
        ApprovalInstanceDetail second = await host.StartAsync(subject, 2, ApprovalTestHost.SingleStage);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal((first.Id, 2, ApprovalInstanceStatus.Pending), (second.PreviousInstanceId!.Value, second.Subject.RevisionNo, second.Status));
        Assert.DoesNotContain(second.Tasks, t => first.Tasks.Any(f => f.Id == t.Id));
        Assert.Equal(before, await host.SnapshotAsync(first.Id));

        await host.ApproveAsync(2, second.Tasks.Single().Id);
        Assert.True(await host.DispatchAsync(await host.OutcomeMessageIdAsync(second.Id)));
        Assert.Equal("APPROVED|2", await host.SourceStateAsync(subject));
        Assert.Equal(before, await host.SnapshotAsync(first.Id));
    }

    [Fact]
    public async Task ReturningOrRejectingNeedsAReason()
    {
        ApprovalInstanceDetail run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.SingleStage);

        foreach (ApprovalTaskDecision decision in new[] { ApprovalTaskDecision.Return, ApprovalTaskDecision.Reject })
        {
            AdministrationResult<ApprovalInstanceDetail> refused = await host.DecideAsync(2, run.Tasks.Single().Id, decision);
            Assert.Equal(ApprovalErrorCodes.ReasonRequired, refused.Error!.Code);
        }
    }

    /// <summary>A retried submission of the same PENDING revision is the same run; another run of the subject waits for it.</summary>
    [Fact]
    public async Task ARetriedStartIsTheSameRunAndANewerRevisionWaits()
    {
        Guid subject = await host.NewSubjectAsync();
        ApprovalInstanceDetail run = await host.StartAsync(subject, 1, ApprovalTestHost.SingleStage);

        Assert.Equal(run.Id, (await host.StartAsync(subject, 1, ApprovalTestHost.SingleStage)).Id);
        Assert.Equal(ApprovalErrorCodes.AlreadyPending, (await host.TryStartAsync(subject, 2, ApprovalTestHost.SingleStage)).Error!.Code);
    }

    /// <summary>The database holds a decided run and its tasks as they were decided, whoever writes to it (RETAIN).</summary>
    [Theory]
    [InlineData("UPDATE approval.approval_instance SET status = 'PENDING', completed_at = NULL WHERE id = '{0}'")]
    [InlineData("UPDATE approval.approval_instance SET subject_revision_no = 9 WHERE id = '{0}'")]
    [InlineData("UPDATE approval.approval_instance SET outcome_delivered_at = now() - interval '1 day' WHERE id = '{0}'")]
    [InlineData("UPDATE approval.approval_task SET decision_reason = 'Rewritten.' WHERE approval_instance_id = '{0}'")]
    [InlineData("UPDATE approval.approval_task SET status = 'APPROVED' WHERE approval_instance_id = '{0}'")]
    [InlineData("DELETE FROM approval.approval_task WHERE approval_instance_id = '{0}'")]
    [InlineData("DELETE FROM approval.approval_instance WHERE id = '{0}'")]
    public async Task ADecidedRunIsNeverRewrittenOrDeleted(string statement)
    {
        ApprovalInstanceDetail run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.SingleStage);
        await host.DecideAsync(2, run.Tasks.Single().Id, ApprovalTaskDecision.Return, "Incomplete.");
        Assert.True(await host.DispatchAsync(await host.OutcomeMessageIdAsync(run.Id)));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(string.Format(null, statement, run.Id)));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
    }
}
