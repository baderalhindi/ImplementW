using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Schedule;
using PMPlatform.Domain.Schedule;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Schedule;

/// <summary>The <c>schedule</c> schema (TASK-046).</summary>
internal sealed class ScheduleRepository(PMPlatformDbContext context) : IScheduleRepository
{
    public async Task<IScheduleWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new ScheduleWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new ScheduleWork(null);

    public async Task<ProjectSchedule?> LockScheduleAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The schedule is locked inside a unit of work only.");
        }

        // Read whole, not composed: a locking clause stays outside any subquery EF Core would wrap it in. xmin is a system
        // column, so * leaves out the row version the entity maps.
        return (await context.Set<ProjectSchedule>()
                .FromSql($"SELECT *, xmin FROM schedule.project_schedule WHERE project_id = {projectId} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();
    }

    public Task<ProjectSchedule?> FindScheduleAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Set<ProjectSchedule>().AsNoTracking().SingleOrDefaultAsync(s => s.ProjectId == projectId, cancellationToken);

    public Task<ProjectSchedule?> FindScheduleByIdAsync(Guid projectScheduleId, CancellationToken cancellationToken) =>
        context.Set<ProjectSchedule>().AsNoTracking().SingleOrDefaultAsync(s => s.Id == projectScheduleId, cancellationToken);

    public async Task<IReadOnlyList<ScheduleActivity>> ListActivitiesAsync(Guid projectScheduleId, bool track, CancellationToken cancellationToken)
    {
        IQueryable<ScheduleActivity> rows = context.Set<ScheduleActivity>();
        return await (track ? rows : rows.AsNoTracking()).Where(a => a.ProjectScheduleId == projectScheduleId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<(IReadOnlyList<ScheduleActivity> Items, int TotalCount)> PageActivitiesAsync(Guid projectScheduleId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(
            context.Set<ScheduleActivity>().AsNoTracking().Where(a => a.ProjectScheduleId == projectScheduleId)
                .OrderBy(a => a.SortOrder).ThenBy(a => a.WbsCode).ThenBy(a => a.Id),
            page, cancellationToken);

    public async Task<ScheduleActivity?> FindActivityAsync(Guid activityId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ScheduleActivity? activity = await context.Set<ScheduleActivity>().SingleOrDefaultAsync(a => a.Id == activityId, cancellationToken).ConfigureAwait(false);
        if (activity is not null)
        {
            AdministrationPersistence.ExpectVersion(context, activity, expectedVersion);
        }

        return activity;
    }

    public async Task<IReadOnlyList<ScheduleDependency>> ListDependenciesAsync(Guid projectScheduleId, CancellationToken cancellationToken) =>
        await Dependencies(projectScheduleId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<(IReadOnlyList<ScheduleDependency> Items, int TotalCount)> PageDependenciesAsync(Guid projectScheduleId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(Dependencies(projectScheduleId).OrderBy(d => d.CreatedAt).ThenBy(d => d.Id), page, cancellationToken);

    public Task<ScheduleDependency?> FindDependencyAsync(Guid dependencyId, CancellationToken cancellationToken) =>
        context.Set<ScheduleDependency>().SingleOrDefaultAsync(d => d.Id == dependencyId, cancellationToken);

    public async Task<IReadOnlyList<ProjectBaseline>> ListBaselinesAsync(Guid projectId, bool track, CancellationToken cancellationToken)
    {
        IQueryable<ProjectBaseline> rows = context.Set<ProjectBaseline>();
        return await (track ? rows : rows.AsNoTracking()).Where(b => b.ProjectId == projectId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<(IReadOnlyList<ProjectBaseline> Items, int TotalCount)> PageBaselinesAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<ProjectBaseline>().AsNoTracking().Where(b => b.ProjectId == projectId).OrderByDescending(b => b.VersionNo), page, cancellationToken);

    public async Task<ProjectBaseline?> FindBaselineAsync(Guid baselineId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectBaseline? baseline = await context.Set<ProjectBaseline>().SingleOrDefaultAsync(b => b.Id == baselineId, cancellationToken).ConfigureAwait(false);
        if (baseline is not null)
        {
            AdministrationPersistence.ExpectVersion(context, baseline, expectedVersion);
        }

        return baseline;
    }

    public Task<ProjectBaseline?> FindActiveBaselineAsync(Guid projectId, bool track, CancellationToken cancellationToken)
    {
        IQueryable<ProjectBaseline> rows = context.Set<ProjectBaseline>();
        return (track ? rows : rows.AsNoTracking()).SingleOrDefaultAsync(b => b.ProjectId == projectId && b.Status == ProjectBaselineStatus.Active, cancellationToken);
    }

    public Task<bool> HasDeclaredBaselineAsync(Guid projectIntakeId, CancellationToken cancellationToken) =>
        context.Set<ProjectBaseline>().AnyAsync(b => b.ProjectIntakeId == projectIntakeId, cancellationToken);

    public async Task<IReadOnlyList<BaselineActivity>> ListBaselineActivitiesAsync(Guid baselineId, CancellationToken cancellationToken) =>
        await context.Set<BaselineActivity>().AsNoTracking().Where(a => a.ProjectBaselineId == baselineId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<(IReadOnlyList<BaselineActivity> Items, int TotalCount)> PageBaselineActivitiesAsync(Guid baselineId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(
            context.Set<BaselineActivity>().AsNoTracking().Where(a => a.ProjectBaselineId == baselineId).OrderBy(a => a.PlannedStartDate).ThenBy(a => a.ScheduleActivityId),
            page, cancellationToken);

    public Task<(IReadOnlyList<BaselineDependency> Items, int TotalCount)> PageBaselineDependenciesAsync(Guid baselineId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(
            context.Set<BaselineDependency>().AsNoTracking().Where(d => d.ProjectBaselineId == baselineId).OrderBy(d => d.PredecessorActivityId).ThenBy(d => d.SuccessorActivityId),
            page, cancellationToken);

    public Task<ScheduleHealthStatus?> FindHealthStatusAsync(Guid projectId, bool track, CancellationToken cancellationToken)
    {
        IQueryable<ScheduleHealthStatus> rows = context.Set<ScheduleHealthStatus>();
        return (track ? rows : rows.AsNoTracking()).SingleOrDefaultAsync(h => h.ProjectId == projectId, cancellationToken);
    }

    public uint RowVersionOf(ProjectSchedule schedule) => RowVersion(schedule);

    public uint RowVersionOf(ScheduleActivity activity) => RowVersion(activity);

    public uint RowVersionOf(ScheduleDependency dependency) => RowVersion(dependency);

    public uint RowVersionOf(ProjectBaseline baseline) => RowVersion(baseline);

    public void Add(ProjectSchedule schedule) => context.Add(schedule);

    public void Add(ScheduleActivity activity) => context.Add(activity);

    public void Add(ScheduleDependency dependency) => context.Add(dependency);

    public void Add(ProjectBaseline baseline) => context.Add(baseline);

    public void Add(BaselineActivity baselineActivity) => context.Add(baselineActivity);

    public void Add(BaselineDependency baselineDependency) => context.Add(baselineDependency);

    public void Add(ScheduleHealthStatus healthStatus) => context.Add(healthStatus);

    public void Remove(ScheduleDependency dependency) => context.Remove(dependency);

    public void Remove(ProjectBaseline baseline) => context.Remove(baseline);

    public async Task<ScheduleSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ScheduleSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return ScheduleSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A WBS code, a dependency, a project's schedule, its ACTIVE baseline or an intake's baseline another request wrote first.
            context.ChangeTracker.Clear();
            return ScheduleSaveOutcome.Duplicate;
        }
    }

    /// <summary>The schedule's dependencies: both ends are its activities, so the successor's schedule decides.</summary>
    private IQueryable<ScheduleDependency> Dependencies(Guid projectScheduleId) =>
        context.Set<ScheduleDependency>().AsNoTracking()
            .Where(d => context.Set<ScheduleActivity>().Any(a => a.Id == d.SuccessorActivityId && a.ProjectScheduleId == projectScheduleId));

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
    private sealed class ScheduleWork(IDbContextTransaction? transaction) : IScheduleWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
