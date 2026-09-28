using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMPlatform.Application.Features.Approval;
using PMPlatform.Domain.Approval;
using PMPlatform.Infrastructure.Persistence.Configurations.Approval;

namespace PMPlatform.Infrastructure.Persistence.Approval;

/// <summary>The <c>approval</c> schema (TASK-035).</summary>
internal sealed class ApprovalRepository(PMPlatformDbContext context) : IApprovalRepository
{
    public Task<ApprovalInstance?> FindInstanceAsync(Guid instanceId, CancellationToken cancellationToken) =>
        context.Set<ApprovalInstance>().SingleOrDefaultAsync(i => i.Id == instanceId, cancellationToken);

    public async Task<IReadOnlyList<ApprovalTask>> GetTasksAsync(Guid instanceId, CancellationToken cancellationToken) =>
        await context.Set<ApprovalTask>()
            .Where(t => t.ApprovalInstanceId == instanceId)
            .OrderBy(t => t.SequenceNo).ThenBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ApprovalTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        context.Set<ApprovalTask>().SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken);

    public async Task<IReadOnlyList<ApprovalRun>> FindBySubjectAsync(string subjectModule, string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        List<ApprovalInstance> instances = await context.Set<ApprovalInstance>().AsNoTracking()
            .Where(i => i.SubjectModule == subjectModule && i.SubjectType == subjectType && i.SubjectId == subjectId)
            .OrderBy(i => i.SubjectRevisionNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<Guid> ids = [.. instances.Select(i => i.Id)];
        ILookup<Guid, ApprovalTask> tasks = (await context.Set<ApprovalTask>().AsNoTracking()
                .Where(t => ids.Contains(t.ApprovalInstanceId))
                .OrderBy(t => t.SequenceNo).ThenBy(t => t.CreatedAt).ThenBy(t => t.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToLookup(t => t.ApprovalInstanceId);

        return [.. instances.Select(i => new ApprovalRun(i, [.. tasks[i.Id]]))];
    }

    public async Task<(IReadOnlyList<ApprovalInstance> Items, int TotalCount)> ListRequestedByAsync(
        Guid userId, IReadOnlyCollection<ApprovalInstanceStatus> statuses, int skip, int take, CancellationToken cancellationToken)
    {
        IQueryable<ApprovalInstance> query = context.Set<ApprovalInstance>().AsNoTracking().Where(i => i.RequestedByUserId == userId);
        if (statuses.Count > 0)
        {
            query = query.Where(i => statuses.Contains(i.Status));
        }

        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ApprovalInstance> items = await query
            .OrderByDescending(i => i.RequestedAt).ThenByDescending(i => i.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public async Task<IReadOnlyList<(ApprovalTask Task, ApprovalInstance Instance)>> FindCurrentTasksAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        IQueryable<ApprovalTask> tasks = context.Set<ApprovalTask>().AsNoTracking();
        var rows = await tasks
            .Where(t => t.Status == ApprovalTaskStatus.Pending && roleIds.Contains(t.AssignedRoleId))
            .Where(t => !tasks.Any(o => o.ApprovalInstanceId == t.ApprovalInstanceId && o.Status == ApprovalTaskStatus.Pending && o.SequenceNo < t.SequenceNo))
            .Join(
                context.Set<ApprovalInstance>().AsNoTracking().Where(i => i.Status == ApprovalInstanceStatus.Pending),
                t => t.ApprovalInstanceId,
                i => i.Id,
                (t, i) => new { Task = t, Instance = i })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => (r.Task, r.Instance))];
    }

    public async Task<IReadOnlyList<Guid>> FindOverdueTaskIdsAsync(DateTimeOffset at, int count, CancellationToken cancellationToken)
    {
        IQueryable<ApprovalTask> tasks = context.Set<ApprovalTask>().AsNoTracking();
        return await tasks
            .Where(t => t.Status == ApprovalTaskStatus.Pending && t.DueAt <= at)
            .Where(t => !tasks.Any(o => o.EscalatedToTaskId == t.Id))
            .Where(t => context.Set<ApprovalInstance>().Any(i => i.Id == t.ApprovalInstanceId && i.Status == ApprovalInstanceStatus.Pending))
            .OrderBy(t => t.DueAt).ThenBy(t => t.Id)
            .Select(t => t.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ApprovalDelegation>> FindDelegationsToAsync(Guid delegateUserId, DateTimeOffset at, CancellationToken cancellationToken) =>
        await context.Set<ApprovalDelegation>().AsNoTracking()
            .Where(d => d.DelegateUserId == delegateUserId && d.Status == ApprovalDelegationStatus.Active && d.ValidFrom <= at && d.ValidTo > at)
            .OrderBy(d => d.ValidFrom).ThenBy(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ApprovalDelegation>> ListDelegationsAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Set<ApprovalDelegation>().AsNoTracking()
            .Where(d => d.DelegatorUserId == userId || d.DelegateUserId == userId)
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ApprovalDelegation?> FindDelegationAsync(Guid delegationId, CancellationToken cancellationToken) =>
        context.Set<ApprovalDelegation>().SingleOrDefaultAsync(d => d.Id == delegationId, cancellationToken);

    public async Task<IReadOnlyList<ApprovalDelegation>> FindLapsedDelegationsAsync(DateTimeOffset at, int count, CancellationToken cancellationToken) =>
        await context.Set<ApprovalDelegation>()
            .Where(d => d.Status == ApprovalDelegationStatus.Active && d.ValidTo <= at)
            .OrderBy(d => d.ValidTo).ThenBy(d => d.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(ApprovalInstance instance) => context.Add(instance);

    public void Add(ApprovalTask task) => context.Add(task);

    public void Add(ApprovalDelegation delegation) => context.Add(delegation);

    public async Task<ApprovalSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ApprovalSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return ApprovalSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
                                                  && violation.ConstraintName == ApprovalInstanceConfiguration.SubjectRevisionKey)
        {
            context.ChangeTracker.Clear();
            return ApprovalSaveOutcome.DuplicateRun;
        }
    }
}
