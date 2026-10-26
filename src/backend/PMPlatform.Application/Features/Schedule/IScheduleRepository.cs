using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// The <c>schedule</c> schema (TASK-046). Finds that return rows to change track them; the others do not. Every write runs
/// inside <see cref="BeginAsync"/>'s unit of work after <see cref="LockScheduleAsync"/>, which takes the project's schedule
/// row for update, so the writes to one project's schedule and baselines happen one at a time.
/// </summary>
public interface IScheduleRepository
{
    /// <summary>A transaction for the writes that follow, or the caller's own when one is open already (an outcome dispatch).</summary>
    public Task<IScheduleWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>The project's schedule, locked for update until the unit of work ends, and tracked. Null when it has none.</summary>
    public Task<ProjectSchedule?> LockScheduleAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<ProjectSchedule?> FindScheduleAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<ProjectSchedule?> FindScheduleByIdAsync(Guid projectScheduleId, CancellationToken cancellationToken);

    /// <summary>Every activity of the schedule, cancelled ones included; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<ScheduleActivity>> ListActivitiesAsync(Guid projectScheduleId, bool track, CancellationToken cancellationToken);

    /// <summary>One page of them in sort order, then WBS code, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ScheduleActivity> Items, int TotalCount)> PageActivitiesAsync(Guid projectScheduleId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ScheduleActivity?> FindActivityAsync(Guid activityId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The activity and its schedule's project, not tracked: what WF-04 reads (edge 9). Null when there is no such activity.</summary>
    public Task<(ScheduleActivity Activity, Guid ProjectId)?> ReadActivityAsync(Guid activityId, CancellationToken cancellationToken);

    /// <summary>Every dependency between the schedule's activities. Not tracked.</summary>
    public Task<IReadOnlyList<ScheduleDependency>> ListDependenciesAsync(Guid projectScheduleId, CancellationToken cancellationToken);

    /// <summary>One page of them, oldest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ScheduleDependency> Items, int TotalCount)> PageDependenciesAsync(Guid projectScheduleId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<ScheduleDependency?> FindDependencyAsync(Guid dependencyId, CancellationToken cancellationToken);

    /// <summary>Every milestone of the schedule, cancelled ones included. Not tracked.</summary>
    public Task<IReadOnlyList<ProjectMilestone>> ListMilestonesAsync(Guid projectScheduleId, CancellationToken cancellationToken);

    /// <summary>One page of the project's milestones, earliest forecast first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ProjectMilestone> Items, int TotalCount)> PageMilestonesAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ProjectMilestone?> FindMilestoneAsync(Guid milestoneId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Not tracked: what WF-05 reads (edge 10).</summary>
    public Task<ProjectMilestone?> ReadMilestoneAsync(Guid milestoneId, CancellationToken cancellationToken);

    /// <summary>Every baseline of the project; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<ProjectBaseline>> ListBaselinesAsync(Guid projectId, bool track, CancellationToken cancellationToken);

    /// <summary>One page of them, latest version first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ProjectBaseline> Items, int TotalCount)> PageBaselinesAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ProjectBaseline?> FindBaselineAsync(Guid baselineId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The project's ACTIVE baseline; tracked when <paramref name="track"/>.</summary>
    public Task<ProjectBaseline?> FindActiveBaselineAsync(Guid projectId, bool track, CancellationToken cancellationToken);

    public Task<bool> HasDeclaredBaselineAsync(Guid projectIntakeId, CancellationToken cancellationToken);

    /// <summary>What the baseline froze. Not tracked.</summary>
    public Task<IReadOnlyList<BaselineActivity>> ListBaselineActivitiesAsync(Guid baselineId, CancellationToken cancellationToken);

    public Task<(IReadOnlyList<BaselineActivity> Items, int TotalCount)> PageBaselineActivitiesAsync(Guid baselineId, PageRequest page, CancellationToken cancellationToken);

    public Task<(IReadOnlyList<BaselineDependency> Items, int TotalCount)> PageBaselineDependenciesAsync(Guid baselineId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The milestone dates the baseline froze. Not tracked.</summary>
    public Task<IReadOnlyList<BaselineMilestone>> ListBaselineMilestonesAsync(Guid baselineId, CancellationToken cancellationToken);

    public Task<(IReadOnlyList<BaselineMilestone> Items, int TotalCount)> PageBaselineMilestonesAsync(Guid baselineId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The project's live Schedule Health row; tracked when <paramref name="track"/>.</summary>
    public Task<ScheduleHealthStatus?> FindHealthStatusAsync(Guid projectId, bool track, CancellationToken cancellationToken);

    /// <summary>The live Schedule Health row of each of the projects that has one (FG-01, TASK-069). Not tracked.</summary>
    public Task<IReadOnlyList<ScheduleHealthStatus>> ListHealthStatusesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked row, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(ProjectSchedule schedule);

    public uint RowVersionOf(ScheduleActivity activity);

    public uint RowVersionOf(ScheduleDependency dependency);

    public uint RowVersionOf(ProjectBaseline baseline);

    public uint RowVersionOf(ProjectMilestone milestone);

    public void Add(ProjectSchedule schedule);

    public void Add(ScheduleActivity activity);

    public void Add(ScheduleDependency dependency);

    public void Add(ProjectBaseline baseline);

    public void Add(BaselineActivity baselineActivity);

    public void Add(BaselineDependency baselineDependency);

    public void Add(ScheduleHealthStatus healthStatus);

    public void Add(ProjectMilestone milestone);

    public void Add(BaselineMilestone baselineMilestone);

    /// <summary>HARD_WORKING.</summary>
    public void Remove(ScheduleDependency dependency);

    /// <summary>HARD_DRAFT.</summary>
    public void Remove(ProjectBaseline baseline);

    /// <summary>
    /// Saves the tracked changes and the audit events this unit of work staged. A row changed since it was read, and a unique
    /// key another request took first, are answers, not faults; after either nothing stays tracked and the unit of work is lost.
    /// </summary>
    public Task<ScheduleSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A unit of work over the schedule; disposing it without <see cref="CommitAsync"/> rolls it back.</summary>
public interface IScheduleWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}
