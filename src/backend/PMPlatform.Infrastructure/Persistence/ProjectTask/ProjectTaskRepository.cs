using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ProjectTask;
using PMPlatform.Domain.ProjectTask;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Infrastructure.Persistence.ProjectTask;

/// <summary>The <c>project_task</c> schema (TASK-048).</summary>
internal sealed class ProjectTaskRepository(PMPlatformDbContext context) : IProjectTaskRepository
{
    public async Task<IProjectTaskWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new ProjectTaskWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new ProjectTaskWork(null);

    public async Task LockProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A project's tasks are locked inside a unit of work only.");
        }

        // The schema has no row per project to lock, so a transaction-scoped advisory lock on the project stands for one;
        // project_task.lock_project() is the same key the database guards take.
        await context.Database.ExecuteSqlAsync($"SELECT project_task.lock_project({projectId})", cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProjectTaskEntity>> ListTasksAsync(Guid projectId, bool track, CancellationToken cancellationToken)
    {
        IQueryable<ProjectTaskEntity> rows = context.Set<ProjectTaskEntity>();
        return await (track ? rows : rows.AsNoTracking()).Where(t => t.ProjectId == projectId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<(IReadOnlyList<ProjectTaskEntity> Items, int TotalCount)> PageTasksAsync(
        Guid projectId, Guid? assigneeUserId, PageRequest page, CancellationToken cancellationToken)
    {
        IQueryable<ProjectTaskEntity> rows = context.Set<ProjectTaskEntity>().AsNoTracking().Where(t => t.ProjectId == projectId);
        if (assigneeUserId is { } assignee)
        {
            rows = rows.Where(t => t.AssigneeUserId == assignee);
        }

        // Identifiers are UUIDv7, so ordering by them is creation order: each parent, then its subtasks.
        return PageAsync(rows.OrderBy(t => t.ParentTaskId ?? t.Id).ThenBy(t => t.ParentTaskId != null).ThenBy(t => t.Id), page, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectTaskEntity>> ListSubtasksAsync(IReadOnlyCollection<Guid> parentTaskIds, CancellationToken cancellationToken) =>
        await context.Set<ProjectTaskEntity>().AsNoTracking()
            .Where(t => t.ParentTaskId != null && parentTaskIds.Contains(t.ParentTaskId.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ProjectTaskEntity?> FindTaskAsync(Guid taskId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectTaskEntity? task = await context.Set<ProjectTaskEntity>().SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken).ConfigureAwait(false);
        if (task is not null)
        {
            AdministrationPersistence.ExpectVersion(context, task, expectedVersion);
        }

        return task;
    }

    public async Task<IReadOnlyList<TaskDependency>> ListDependenciesAsync(Guid projectId, CancellationToken cancellationToken) =>
        await Dependencies(projectId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<(IReadOnlyList<TaskDependency> Items, int TotalCount)> PageDependenciesAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(Dependencies(projectId).OrderBy(d => d.CreatedAt).ThenBy(d => d.Id), page, cancellationToken);

    public Task<TaskDependency?> FindDependencyAsync(Guid dependencyId, CancellationToken cancellationToken) =>
        context.Set<TaskDependency>().SingleOrDefaultAsync(d => d.Id == dependencyId, cancellationToken);

    public async Task<IReadOnlyList<ActivityExecutionProgress>> ListActivityProgressAsync(Guid projectId, CancellationToken cancellationToken) =>
        await context.Set<ActivityExecutionProgress>().Where(p => p.ProjectId == projectId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<(IReadOnlyList<ActivityExecutionProgress> Items, int TotalCount)> PageActivityProgressAsync(
        Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(
            context.Set<ActivityExecutionProgress>().AsNoTracking().Where(p => p.ProjectId == projectId).OrderBy(p => p.ScheduleActivityId),
            page, cancellationToken);

    public uint RowVersionOf(ProjectTaskEntity task) => RowVersion(task);

    public uint RowVersionOf(TaskDependency dependency) => RowVersion(dependency);

    public void Add(ProjectTaskEntity task) => context.Add(task);

    public void Add(TaskDependency dependency) => context.Add(dependency);

    public void Add(ActivityExecutionProgress progress) => context.Add(progress);

    public void Remove(TaskDependency dependency) => context.Remove(dependency);

    public async Task<ProjectTaskSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ProjectTaskSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return ProjectTaskSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A dependency, or an activity's progress row, another request wrote first.
            context.ChangeTracker.Clear();
            return ProjectTaskSaveOutcome.Duplicate;
        }
    }

    /// <summary>The project's dependencies: both ends are its tasks, so the successor's project decides.</summary>
    private IQueryable<TaskDependency> Dependencies(Guid projectId) =>
        context.Set<TaskDependency>().AsNoTracking()
            .Where(d => context.Set<ProjectTaskEntity>().Any(t => t.Id == d.SuccessorTaskId && t.ProjectId == projectId));

    private uint RowVersion<TRow>(TRow row)
        where TRow : class => context.Entry(row).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    private static async Task<(IReadOnlyList<T> Items, int TotalCount)> PageAsync<T>(IQueryable<T> rows, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<T> items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    /// <summary>Owns its transaction when it began one; joined to the caller's otherwise, which the caller commits.</summary>
    private sealed class ProjectTaskWork(IDbContextTransaction? transaction) : IProjectTaskWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
