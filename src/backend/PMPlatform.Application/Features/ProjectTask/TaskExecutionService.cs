using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// A task's execution along <see cref="ProjectTaskWorkflow"/> (TASK-048). Each command is decided on its own permission, finds its
/// edge in the state machine or is refused 409, then checks the board under the project's task lock: the dependencies that
/// gate a start or a completion, and the subtasks a parent waits for. Execution needs an ACTIVE project; cancelling is
/// planning and needs it APPROVED_PLANNED or ACTIVE.
/// </summary>
internal sealed class TaskExecutionService(
    IProjectTaskRepository repository, ProjectTaskGate gate, IAuditTrail audit, TimeProvider timeProvider) : ITaskExecutionService
{
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> StartAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, taskId, TaskCommand.Start, null, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> BlockAsync(
        Guid callerId, Guid taskId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return MoveAsync(callerId, taskId, TaskCommand.Block, reason, expectedVersion, cancellationToken);
    }

    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> UnblockAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, taskId, TaskCommand.Unblock, null, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> CompleteAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, taskId, TaskCommand.Complete, null, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> ReopenAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, taskId, TaskCommand.Reopen, null, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> CancelAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, taskId, TaskCommand.Cancel, null, expectedVersion, cancellationToken);

    public async Task<AdministrationResult<Versioned<ProjectTaskDetail>>> ReportProgressAsync(
        Guid callerId, Guid taskId, decimal actualPercentComplete, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actualPercentComplete);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(actualPercentComplete, 100m);

        Loaded loaded = await gate.LoadTaskAsync(callerId, PermissionCatalogue.TaskUpdate, taskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        if (ProjectTaskReferences.ExecutionRefused(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IProjectTaskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        TaskBoard board = await gate.OpenAsync(project, cancellationToken).ConfigureAwait(false);
        ProjectTaskEntity task = board.Find(taskId)!;
        if (task.Status is not (ProjectTaskStatus.InProgress or ProjectTaskStatus.Blocked) || board.HasLiveSubtasks(task.Id))
        {
            return AdministrationError.Rule(ProjectTaskErrorCodes.ProgressNotEnterable);
        }

        decimal? before = task.ActualPercentComplete;
        DateTimeOffset now = timeProvider.GetUtcNow();
        task.ActualPercentComplete = actualPercentComplete;
        TaskBoard.Touch(task, callerId, now);
        audit.Stage(ProjectTaskAudit.ProgressReported(callerId, project, before, task));
        return await gate.SaveTaskAsync(work, board, task, callerId, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdministrationResult<Versioned<ProjectTaskDetail>>> MoveAsync(
        Guid callerId, Guid taskId, TaskCommand command, NarrativeText? reason, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await gate.LoadTaskAsync(callerId, ProjectTaskWorkflow.PermissionOf(command), taskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        AdministrationError? notEligible = command == TaskCommand.Cancel ? ProjectTaskReferences.PlanningRefused(project) : ProjectTaskReferences.ExecutionRefused(project);
        if (notEligible is not null)
        {
            return notEligible;
        }

        await using IProjectTaskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        TaskBoard board = await gate.OpenAsync(project, cancellationToken).ConfigureAwait(false);
        ProjectTaskEntity task = board.Find(taskId)!;
        ProjectTaskStatus from = task.Status;
        if (ProjectTaskWorkflow.TargetOf(command, from, TaskDependencyRules.HasStarted(task)) is not { } to)
        {
            return from == ProjectTaskStatus.Cancelled ? AdministrationError.Conflict(ProjectTaskErrorCodes.NotEditable) : AdministrationError.InvalidTransition;
        }

        if (RuleRefusal(board, task, command) is { } broken)
        {
            return broken;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        task.Status = to;
        task.BlockedReason = to == ProjectTaskStatus.Blocked ? reason : null;
        switch (command)
        {
            case TaskCommand.Start:
                task.ActualStartDate = today;
                break;
            case TaskCommand.Complete:
                task.ActualFinishDate = today;
                task.CompletedAt = now;
                task.ActualPercentComplete = board.HasLiveSubtasks(task.Id) ? task.ActualPercentComplete : 100m;
                break;
            case TaskCommand.Reopen:
                task.ActualFinishDate = null;
                task.CompletedAt = null;
                task.ReopenedCount++;
                break;
            case TaskCommand.Block:
            case TaskCommand.Unblock:
            case TaskCommand.Cancel:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown task command.");
        }

        TaskBoard.Touch(task, callerId, now);
        audit.Stage(ProjectTaskAudit.Transition(EventOf(command), callerId, project, from, task));
        return await gate.SaveTaskAsync(work, board, task, callerId, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The board's rules for the command: dependencies gate a start or a completion, subtasks a parent's completion, and a task in use is not cancelled.</summary>
    private static AdministrationError? RuleRefusal(TaskBoard board, ProjectTaskEntity task, TaskCommand command) => command switch
    {
        TaskCommand.Start => DependencyRefusal(board, task, start: true),
        TaskCommand.Complete => board.SubtasksOf(task.Id).Any(s => ProjectTaskWorkflow.IsLive(s.Status) && s.Status != ProjectTaskStatus.Completed)
            ? AdministrationError.Conflict(ProjectTaskErrorCodes.SubtasksOpen)
            : DependencyRefusal(board, task, start: false),
        TaskCommand.Reopen => task.ParentTaskId is { } parentId && board.Find(parentId)!.Status == ProjectTaskStatus.Completed
            ? AdministrationError.Conflict(ProjectTaskErrorCodes.SubtasksOpen)
            : null,
        TaskCommand.Cancel => board.HasLiveSubtasks(task.Id) || board.IsDependencyEnd(task.Id) ? AdministrationError.Conflict(ProjectTaskErrorCodes.TaskInUse) : null,
        TaskCommand.Block or TaskCommand.Unblock => null,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown task command."),
    };

    private static AdministrationError? DependencyRefusal(TaskBoard board, ProjectTaskEntity task, bool start) =>
        TaskDependencyRules.Unmet(task.Id, start, board.Dependencies, board.ById()).Count > 0 ? AdministrationError.Rule(ProjectTaskErrorCodes.DependencyUnmet) : null;

    private static string EventOf(TaskCommand command) => command switch
    {
        TaskCommand.Start => ProjectTaskAuditEvents.TaskStarted,
        TaskCommand.Block => ProjectTaskAuditEvents.TaskBlocked,
        TaskCommand.Unblock => ProjectTaskAuditEvents.TaskUnblocked,
        TaskCommand.Complete => ProjectTaskAuditEvents.TaskCompleted,
        TaskCommand.Reopen => ProjectTaskAuditEvents.TaskReopened,
        TaskCommand.Cancel => ProjectTaskAuditEvents.TaskCancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown task command."),
    };
}
