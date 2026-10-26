using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// The way into a project's tasks every WF-04 operation shares: the project's facts (edge 36), the authorization check, the
/// project's task lock with the whole board read under it, and the save that rolls the Activity Execution Progress up again
/// and commits. A task the caller may not see is one that does not exist (R-47).
/// </summary>
internal sealed class ProjectTaskGate(
    IProjectTaskRepository repository, IProjectFactsReader projects, ProjectTaskAccess access, ActivityProgressProjection progress)
{
    /// <summary>Whether the caller may see the project's tasks owned by <paramref name="assigneeUserId"/> (null: every task).</summary>
    public async Task<ProjectFacts?> ViewableProjectAsync(Guid callerId, Guid projectId, Guid? assigneeUserId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanViewAsync(callerId, project, assigneeUserId, cancellationToken).ConfigureAwait(false)
            ? project
            : null;

    /// <summary>The project, if the caller holds <paramref name="permissionCode"/> on it as a whole.</summary>
    public async Task<Reached> ReachProjectAsync(Guid callerId, string permissionCode, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectFacts? project = await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        return project is null ? new Reached(null, AdministrationError.NotFound)
            : new Reached(project, await access.CheckAsync(callerId, permissionCode, project, null, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The task, tracked, and its project, if the caller holds <paramref name="permissionCode"/> on it: as the project's or as its owner.</summary>
    public async Task<Loaded> LoadTaskAsync(Guid callerId, string permissionCode, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectTaskEntity? task = await repository.FindTaskAsync(taskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = task is null ? null : await projects.FindAsync(task.ProjectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return new Loaded(null, null, AdministrationError.NotFound);
        }

        AdministrationError? refused = await access.CheckAsync(callerId, permissionCode, project, task!.AssigneeUserId, cancellationToken).ConfigureAwait(false);
        return new Loaded(project, task, refused);
    }

    /// <summary>Takes the project's task lock and reads the board whole. Call inside a unit of work.</summary>
    public async Task<TaskBoard> OpenAsync(ProjectFacts project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        await repository.LockProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);
        List<ProjectTaskEntity> tasks = [.. await repository.ListTasksAsync(project.Id, track: true, cancellationToken).ConfigureAwait(false)];
        List<TaskDependency> dependencies = [.. await repository.ListDependenciesAsync(project.Id, cancellationToken).ConfigureAwait(false)];
        return new TaskBoard(project, tasks, dependencies);
    }

    /// <summary>Rolls the board's activities up again, saves, and commits what was saved.</summary>
    public async Task<ProjectTaskSaveOutcome> SaveAsync(IProjectTaskWork work, TaskBoard board, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        await progress.StageAsync(actorId, board, now, cancellationToken).ConfigureAwait(false);
        ProjectTaskSaveOutcome outcome = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (outcome == ProjectTaskSaveOutcome.Saved)
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return outcome;
    }

    /// <summary>
    /// Saves a change to <paramref name="task"/> and answers with it as now saved. The board is written under the project's lock,
    /// so a row that changed meanwhile is a stale version (412), whichever unique key or row version caught it.
    /// </summary>
    public async Task<AdministrationResult<Versioned<ProjectTaskDetail>>> SaveTaskAsync(
        IProjectTaskWork work, TaskBoard board, ProjectTaskEntity task, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(board);
        return await SaveAsync(work, board, actorId, now, cancellationToken).ConfigureAwait(false) == ProjectTaskSaveOutcome.Saved
            ? new Versioned<ProjectTaskDetail>(ProjectTaskMapping.ToDetail(task, board.Tasks), repository.RowVersionOf(task))
            : AdministrationError.PreconditionFailed;
    }
}

internal sealed record Reached(ProjectFacts? Project, AdministrationError? Error);

internal sealed record Loaded(ProjectFacts? Project, ProjectTaskEntity? Task, AdministrationError? Error);
