using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// WF-04's task plan (TASK-048): tasks and one level of subtasks, their schedule activity, owner, priority and planned dates,
/// and the Activity Execution Progress rolled up from them. Every write locks the project's tasks, checks the change against
/// the whole board and rolls the progress up again in the same transaction.
/// </summary>
internal sealed class ProjectTaskService(
    IProjectTaskRepository repository, ProjectTaskGate gate, ProjectTaskReferences references, IAuditTrail audit, TimeProvider timeProvider) : IProjectTaskService
{
    public async Task<ProjectTaskPage> ListTasksAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        // The project's whole board to whoever may see it; otherwise the tasks the caller owns, if their grant reaches those.
        Guid? assignee = null;
        if (await gate.ViewableProjectAsync(callerId, projectId, null, cancellationToken).ConfigureAwait(false) is null)
        {
            assignee = callerId;
            if (await gate.ViewableProjectAsync(callerId, projectId, assignee, cancellationToken).ConfigureAwait(false) is null)
            {
                return new ProjectTaskPage([], page.Page, page.PageSize, 0);
            }
        }

        (IReadOnlyList<ProjectTaskEntity> items, int total) = await repository.PageTasksAsync(projectId, assignee, page, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ProjectTaskEntity> subtasks = await repository.ListSubtasksAsync([.. items.Select(t => t.Id)], cancellationToken).ConfigureAwait(false);
        return new ProjectTaskPage([.. items.Select(t => ProjectTaskMapping.ToDetail(t, subtasks))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ProjectTaskDetail>>> GetTaskAsync(Guid callerId, Guid taskId, CancellationToken cancellationToken)
    {
        Loaded loaded = await gate.LoadTaskAsync(callerId, PermissionCatalogue.TaskView, taskId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        IReadOnlyList<ProjectTaskEntity> subtasks = await repository.ListSubtasksAsync([taskId], cancellationToken).ConfigureAwait(false);
        return new Versioned<ProjectTaskDetail>(ProjectTaskMapping.ToDetail(loaded.Task!, subtasks), repository.RowVersionOf(loaded.Task!));
    }

    public async Task<AdministrationResult<Versioned<ProjectTaskDetail>>> CreateTaskAsync(Guid callerId, ProjectTaskDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Reached reached = await gate.ReachProjectAsync(callerId, PermissionCatalogue.TaskManage, draft.ProjectId, cancellationToken).ConfigureAwait(false);
        if (reached.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = reached.Project!;
        if (ProjectTaskReferences.PlanningRefused(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IProjectTaskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        TaskBoard board = await gate.OpenAsync(project, cancellationToken).ConfigureAwait(false);
        Guid? activityId = draft.ScheduleActivityId;
        if (draft.ParentTaskId is { } parentId)
        {
            if (ParentRefused(board, parentId) is { } badParent)
            {
                return badParent;
            }

            // A subtask executes against its parent's activity (ERD §5.7).
            Guid? parentActivity = board.Find(parentId)!.ScheduleActivityId;
            if (activityId is not null && activityId != parentActivity)
            {
                return AdministrationError.Rule(ProjectTaskErrorCodes.ScheduleActivityInvalid, new FieldIssue("scheduleActivityId", FieldIssue.NotAllowed));
            }

            activityId = parentActivity;
        }

        if (await references.CheckAsync(project, activityId, draft.AssigneeUserId, draft.PriorityItemId, cancellationToken).ConfigureAwait(false) is { } badReference)
        {
            return badReference;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectTaskEntity task = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ScheduleActivityId = activityId,
            ParentTaskId = draft.ParentTaskId,
            Title = draft.Title,
            Description = draft.Description,
            AssigneeUserId = draft.AssigneeUserId,
            PriorityItemId = draft.PriorityItemId,
            Status = ProjectTaskStatus.NotStarted,
            PlannedStartDate = draft.PlannedStartDate,
            PlannedFinishDate = draft.PlannedFinishDate,
            PlannedDurationDays = DurationOf(draft.PlannedStartDate, draft.PlannedFinishDate),
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(task);
        board.Tasks.Add(task);
        audit.Stage(ProjectTaskAudit.Created(callerId, project, task));
        return await gate.SaveTaskAsync(work, board, task, callerId, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProjectTaskDetail>>> UpdateTaskAsync(
        Guid callerId, Guid taskId, ProjectTaskChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Loaded loaded = await gate.LoadTaskAsync(callerId, PermissionCatalogue.TaskManage, taskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        if (ProjectTaskReferences.PlanningRefused(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IProjectTaskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        TaskBoard board = await gate.OpenAsync(project, cancellationToken).ConfigureAwait(false);
        ProjectTaskEntity task = board.Find(taskId)!;
        if (!ProjectTaskWorkflow.IsLive(task.Status))
        {
            return AdministrationError.Conflict(ProjectTaskErrorCodes.NotEditable);
        }

        Guid? activityId = changes.ScheduleActivityId;
        if (task.ParentTaskId is { } parentId)
        {
            Guid? parentActivity = board.Find(parentId)!.ScheduleActivityId;
            if (activityId is not null && activityId != parentActivity)
            {
                return AdministrationError.Rule(ProjectTaskErrorCodes.ScheduleActivityInvalid, new FieldIssue("scheduleActivityId", FieldIssue.NotAllowed));
            }

            activityId = parentActivity;
        }

        // A reference the task already holds stays valid: an owner who has since lost their role does not block an edit of the dates.
        if (await references.CheckAsync(
                project,
                activityId == task.ScheduleActivityId ? null : activityId,
                changes.AssigneeUserId == task.AssigneeUserId ? null : changes.AssigneeUserId,
                changes.PriorityItemId == task.PriorityItemId ? null : changes.PriorityItemId,
                cancellationToken).ConfigureAwait(false) is { } badReference)
        {
            return badReference;
        }

        TaskPlan before = TaskPlan.Of(task);
        DateTimeOffset now = timeProvider.GetUtcNow();
        task.ScheduleActivityId = activityId;
        task.Title = changes.Title;
        task.Description = changes.Description;
        task.AssigneeUserId = changes.AssigneeUserId;
        task.PriorityItemId = changes.PriorityItemId;
        task.PlannedStartDate = changes.PlannedStartDate;
        task.PlannedFinishDate = changes.PlannedFinishDate;
        task.PlannedDurationDays = DurationOf(changes.PlannedStartDate, changes.PlannedFinishDate);
        TaskBoard.Touch(task, callerId, now);

        // The subtasks follow their parent to its new activity.
        foreach (ProjectTaskEntity subtask in board.SubtasksOf(task.Id).Where(s => s.ScheduleActivityId != activityId && ProjectTaskWorkflow.IsLive(s.Status)))
        {
            subtask.ScheduleActivityId = activityId;
            TaskBoard.Touch(subtask, callerId, now);
        }

        audit.Stage(ProjectTaskAudit.Changed(callerId, project, before, task));
        return await gate.SaveTaskAsync(work, board, task, callerId, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ActivityExecutionProgressPage> ListActivityProgressAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, projectId, null, cancellationToken).ConfigureAwait(false) is null)
        {
            return new ActivityExecutionProgressPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ActivityExecutionProgress> items, int total) = await repository.PageActivityProgressAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        return new ActivityExecutionProgressPage([.. items.Select(ProjectTaskMapping.ToDetail)], page.Page, page.PageSize, total);
    }

    /// <summary>
    /// The planned duration, the ADR-009 weight: the days from start to finish, both included. No working calendar is configured,
    /// so every day is a working day, as in WF-03 (schedule-baseline.md F-3).
    /// </summary>
    internal static int DurationOf(DateOnly start, DateOnly finish) => finish.DayNumber - start.DayNumber + 1;

    /// <summary>
    /// A parent is a live, not completed, top-level task of the project — one level of subtasks — that is no dependency end, since
    /// a dependency joins leaves and the parent stops being one.
    /// </summary>
    private static AdministrationError? ParentRefused(TaskBoard board, Guid parentId)
    {
        ProjectTaskEntity? parent = board.Find(parentId);
        return parent is null || !ProjectTaskWorkflow.IsLive(parent.Status) || parent.Status == ProjectTaskStatus.Completed
            ? AdministrationError.Rule(ProjectTaskErrorCodes.HierarchyInvalid, new FieldIssue("parentTaskId", FieldIssue.NotFound))
            : parent.ParentTaskId is not null || board.IsDependencyEnd(parentId)
                ? AdministrationError.Rule(ProjectTaskErrorCodes.HierarchyInvalid, new FieldIssue("parentTaskId", FieldIssue.NotAllowed))
                : null;
    }
}
