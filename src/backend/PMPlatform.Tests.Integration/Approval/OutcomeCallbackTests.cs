using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Tests.Integration.Approval;

/// <summary>
/// TASK-035 acceptance criterion 1 and its validation check: an approval decision calls back to the source module
/// exactly once, even under retry — a second dispatch, a concurrent dispatch, a failed delivery retried, the same
/// callback fired again with the same idempotency key, and a retried decision each leave the source changed once.
/// The test source's handler counts every call and forgives nothing, so a count of 1 is the framework's doing.
/// </summary>
[Collection(ApprovalSuite.Name)]
public sealed class OutcomeCallbackTests(ApprovalTestHost host)
{
    [Fact]
    public async Task TheSameOutcomeFiredTwiceWithTheSameKeyChangesTheSourceOnce()
    {
        Guid subject = await host.NewSubjectAsync();
        ApprovalInstanceDetail run = await host.StartAsync(subject, 1, ApprovalTestHost.SingleStage);
        await host.ApproveAsync(2, run.Tasks.Single().Id);
        Guid message = await host.OutcomeMessageIdAsync(run.Id);

        Assert.True(await host.DispatchAsync(message));
        Assert.False(await host.DispatchAsync(message));
        await host.WithScopeAsync(services => services.GetRequiredService<IOutboxDispatcher>().DispatchDueAsync(100, CancellationToken.None));

        // The callback itself fired again with the same key, as an at-least-once transport would.
        string payload = Assert.Single(await host.Database.QueryAsync($"SELECT payload::text FROM common.outbox_message WHERE id = '{message}'"));
        await host.WithScopeAsync(async services =>
        {
            IDomainEventConsumer consumer = services.GetServices<IDomainEventConsumer>().Single(c => c.EventType == "Approval.ApprovalOutcomeRecorded");
            await consumer.HandleAsync(payload, CancellationToken.None);
            return true;
        });

        Assert.Equal("APPROVED|1", await host.SourceStateAsync(subject));
        Assert.Equal(
            ["APPROVED|true"],
            await host.Database.QueryAsync($"SELECT status || '|' || (outcome_delivered_at IS NOT NULL) FROM approval.approval_instance WHERE id = '{run.Id}'"));
        Assert.Equal(
            ["1|1"],
            await host.Database.QueryAsync($"""
                SELECT count(*) FILTER (WHERE event_type = 'Approval.ApprovalCompleted') || '|' || count(*) FILTER (WHERE event_type = 'Approval.OutcomeDelivered')
                FROM audit_activity.audit_event WHERE subject_id = '{run.Id}'
                """));
    }

    /// <summary>Two API instances, or a worker and a manual redrive, dispatching the same message at once.</summary>
    [Fact]
    public async Task ConcurrentDispatchesOfOneOutcomeDeliverItOnce()
    {
        Guid subject = await host.NewSubjectAsync();
        ApprovalInstanceDetail run = await host.StartAsync(subject, 1, ApprovalTestHost.SingleStage);
        await host.ApproveAsync(2, run.Tasks.Single().Id);
        Guid message = await host.OutcomeMessageIdAsync(run.Id);

        bool[] delivered = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => host.DispatchAsync(message))));

        Assert.Single(delivered, d => d);
        Assert.Equal("APPROVED|1", await host.SourceStateAsync(subject));
    }

    /// <summary>A handler that fails after writing leaves nothing applied; the retry applies the outcome once.</summary>
    [Fact]
    public async Task AFailedDeliveryIsRolledBackAndItsRetryAppliesTheOutcomeOnce()
    {
        Guid subject = await host.NewSubjectAsync();
        ApprovalInstanceDetail run = await host.StartAsync(subject, 1, ApprovalTestHost.SingleStage);
        await host.ApproveAsync(2, run.Tasks.Single().Id);
        Guid message = await host.OutcomeMessageIdAsync(run.Id);
        host.Failures.FailOnce(subject);

        Assert.False(await host.DispatchAsync(message));
        Assert.Equal("SUBMITTED|0", await host.SourceStateAsync(subject));
        Assert.Equal(
            ["1|InvalidOperationException|true|false"],
            await host.Database.QueryAsync($"""
                SELECT attempt_count || '|' || last_error || '|' || (next_attempt_at > now()) || '|' || (dispatched_at IS NOT NULL)
                FROM common.outbox_message WHERE id = '{message}'
                """));
        Assert.Equal(["false"], await host.Database.QueryAsync($"SELECT (outcome_delivered_at IS NOT NULL)::text FROM approval.approval_instance WHERE id = '{run.Id}'"));

        Assert.True(await host.DispatchAsync(message));
        Assert.Equal("APPROVED|1", await host.SourceStateAsync(subject));
        Assert.Equal(["2|true"], await host.Database.QueryAsync($"SELECT attempt_count || '|' || (dispatched_at IS NOT NULL) FROM common.outbox_message WHERE id = '{message}'"));
    }

    /// <summary>A retried decision publishes nothing more, and the outbox refuses a second message with the same key.</summary>
    [Fact]
    public async Task ARetriedDecisionAndARepublishedOutcomeAreRefused()
    {
        Guid subject = await host.NewSubjectAsync();
        ApprovalInstanceDetail run = await host.StartAsync(subject, 1, ApprovalTestHost.SingleStage);
        Guid task = run.Tasks.Single().Id;
        await host.ApproveAsync(2, task);

        AdministrationResult<ApprovalInstanceDetail> retried = await host.DecideAsync(2, task, ApprovalTaskDecision.Approve);
        Assert.Equal(AdministrationErrorKind.TerminalState, retried.Error!.Kind);
        Guid message = await host.OutcomeMessageIdAsync(run.Id);

        PostgresException duplicate = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync($"""
            INSERT INTO common.outbox_message (id, source_module, message_type, message_key, payload, occurred_at, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), source_module, message_type, message_key, payload, now(), now(), created_by, now(), created_by
            FROM common.outbox_message WHERE id = '{message}'
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }
}
