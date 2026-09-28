using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Approval;

/// <summary>The approval runtime driven in process, as a source module and its people reach it; each call is its own scope, as a request is.</summary>
internal static class ApprovalDriver
{
    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    /// <summary>A new approvable record of the test source, at revision 1.</summary>
    public static async Task<Guid> NewSubjectAsync(this ApprovalTestHost host)
    {
        Guid id = Guid.NewGuid();
        await host.Database.ExecuteAsync(TestSource.Insert(id));
        return id;
    }

    /// <summary>What the source module does on submission: start the run, then save its own change and the run together (M-11).</summary>
    public static async Task<AdministrationResult<ApprovalInstanceDetail>> TryStartAsync(
        this ApprovalTestHost host, Guid subjectId, int revisionNo, string routingKey, int requester = 6, Guid? projectId = null)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        AdministrationResult<ApprovalInstanceDetail> result = await scope.ServiceProvider.GetRequiredService<IApprovalRequests>().StartAsync(
            new ApprovalStart(
                new ApprovalSubject(TestSource.Module, TestSource.Type, subjectId, revisionNo), routingKey, Person(requester),
                projectId, Guid.Parse(IdentityDatabase.DepartmentId), null, null, null),
            CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>().SaveChangesAsync();
        return result;
    }

    public static async Task<ApprovalInstanceDetail> StartAsync(
        this ApprovalTestHost host, Guid subjectId, int revisionNo, string routingKey, int requester = 6, Guid? projectId = null)
    {
        AdministrationResult<ApprovalInstanceDetail> result = await host.TryStartAsync(subjectId, revisionNo, routingKey, requester, projectId);
        Assert.True(result.Succeeded, $"Start refused: {result.Error}");
        return result.Value;
    }

    public static async Task<AdministrationResult<ApprovalInstanceDetail>> DecideAsync(
        this ApprovalTestHost host, int person, Guid taskId, ApprovalTaskDecision decision, string? reason = null)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
            Person(person), taskId, decision, reason is null ? null : new NarrativeText(reason, Language.En), CancellationToken.None);
    }

    public static async Task<ApprovalInstanceDetail> ApproveAsync(this ApprovalTestHost host, int person, Guid taskId)
    {
        AdministrationResult<ApprovalInstanceDetail> result = await host.DecideAsync(person, taskId, ApprovalTaskDecision.Approve);
        Assert.True(result.Succeeded, $"Approval refused: {result.Error}");
        return result.Value;
    }

    public static async Task<AdministrationResult<ApprovalDelegationDetail>> DelegateAsync(
        this ApprovalTestHost host, int delegator, int @delegate, string? routingKey = null, TimeSpan? period = null)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IApprovalDelegationService>().CreateAsync(
            Person(delegator),
            new ApprovalDelegationDraft(Person(@delegate), routingKey, null, host.Clock.GetUtcNow() + (period ?? TimeSpan.FromDays(1))),
            CancellationToken.None);
    }

    public static async Task<T> WithScopeAsync<T>(this ApprovalTestHost host, Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public static Task<bool> DispatchAsync(this ApprovalTestHost host, Guid messageId) =>
        host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(messageId, CancellationToken.None);

    public static Task<int> RunMaintenanceAsync(this ApprovalTestHost host) =>
        host.WithScopeAsync(services => services.GetRequiredService<IApprovalMaintenance>().RunAsync(100, CancellationToken.None));

    /// <summary>The outbox row of the run's outcome; fails unless there is exactly one.</summary>
    public static async Task<Guid> OutcomeMessageIdAsync(this ApprovalTestHost host, Guid instanceId) =>
        Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{instanceId}-outcome'")));

    public static async Task<string> SourceStateAsync(this ApprovalTestHost host, Guid subjectId) =>
        Assert.Single(await host.Database.QueryAsync(TestSource.StateOf(subjectId)));

    /// <summary>A run and its tasks as the database holds them, to compare before and after.</summary>
    public static Task<IReadOnlyList<string>> SnapshotAsync(this ApprovalTestHost host, Guid instanceId) =>
        host.Database.QueryAsync($"""
            SELECT to_jsonb(i)::text FROM approval.approval_instance i WHERE i.id = '{instanceId}'
            UNION ALL
            SELECT t.text FROM (SELECT to_jsonb(t)::text AS text FROM approval.approval_task t WHERE t.approval_instance_id = '{instanceId}' ORDER BY t.id) t
            """);
}
