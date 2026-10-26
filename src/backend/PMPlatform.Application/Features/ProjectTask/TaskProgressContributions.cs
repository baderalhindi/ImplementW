using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// Edge 19's WF-04 side (TASK-066): an accepted external report of a task's percentage, entered under WF-04's own rules. The row version
/// is read again under the project's task lock, so no WF-04 writer can change the task between the check and the commit, and the task's
/// row version fails the caller's save if anything else did.
/// </summary>
internal sealed class TaskProgressContributions(
    IProjectTaskRepository repository, IProjectFactsReader projects, ProjectTaskGate gate, ActivityProgressProjection progress, IAuditTrail audit, TimeProvider timeProvider)
    : ITaskProgressContributions
{
    public async Task<TaskProgressSource?> FindAsync(Guid taskId, CancellationToken cancellationToken) =>
        await repository.ReadTaskAsync(taskId, cancellationToken).ConfigureAwait(false) is var (task, version)
            ? new TaskProgressSource(task.Id, task.ProjectId, task.Status, version, task.Title)
            : null;

    public async Task<TaskProgressApplication> StageAsync(TaskProgressContribution contribution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        ArgumentOutOfRangeException.ThrowIfNegative(contribution.ActualPercentComplete);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(contribution.ActualPercentComplete, 100m);

        if (await repository.ReadTaskAsync(contribution.TaskId, cancellationToken).ConfigureAwait(false) is not var (found, _)
            || await projects.FindAsync(found.ProjectId, cancellationToken).ConfigureAwait(false) is not { } project)
        {
            return new TaskProgressApplication(TaskProgressApplicationOutcome.TaskNotFound);
        }

        TaskBoard board = await gate.OpenAsync(project, cancellationToken).ConfigureAwait(false);
        (ProjectTaskEntity stored, uint version) = (await repository.ReadTaskAsync(contribution.TaskId, cancellationToken).ConfigureAwait(false))!.Value;
        if (stored.Status == ProjectTaskStatus.Cancelled)
        {
            return new TaskProgressApplication(TaskProgressApplicationOutcome.TaskCancelled);
        }

        if (version != contribution.ExpectedVersion)
        {
            return new TaskProgressApplication(TaskProgressApplicationOutcome.VersionChanged, version);
        }

        if (ProjectTaskReferences.ExecutionRefused(project) is not null)
        {
            return new TaskProgressApplication(TaskProgressApplicationOutcome.ProjectNotEligible);
        }

        ProjectTaskEntity task = board.Find(contribution.TaskId)!;
        if (task.Status is not (ProjectTaskStatus.InProgress or ProjectTaskStatus.Blocked) || board.HasLiveSubtasks(task.Id))
        {
            return new TaskProgressApplication(TaskProgressApplicationOutcome.ProgressNotEnterable);
        }

        decimal? before = task.ActualPercentComplete;
        DateTimeOffset now = timeProvider.GetUtcNow();
        task.ActualPercentComplete = contribution.ActualPercentComplete;
        TaskBoard.Touch(task, contribution.ActorId, now);
        audit.Stage(ProjectTaskAudit.ProgressContributed(contribution.ActorId, project, before, task, contribution.Lineage));
        await progress.StageAsync(contribution.ActorId, board, now, cancellationToken).ConfigureAwait(false);
        return new TaskProgressApplication(TaskProgressApplicationOutcome.Staged);
    }
}
