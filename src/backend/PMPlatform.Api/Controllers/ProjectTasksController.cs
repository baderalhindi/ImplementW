using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Api.Models.ProjectTask;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-04 (TASK-048): a project's tasks and subtasks — SCR-047 Project Tasks, SCR-063–066 — and their execution along the task
/// state machine. Each command is one edge (R-4); there is no BLOCKED → COMPLETED, and reopen and cancel are controlled by
/// permissions of their own.
/// </summary>
[Route(Collection)]
[Tags("ProjectTask")]
public sealed class ProjectTasksController(IProjectTaskService tasks, ITaskExecutionService execution) : AdministrationControllerBase
{
    private const string Collection = "api/v1/project-tasks";

    /// <summary>
    /// The project's tasks, each parent followed by its subtasks. <c>projectId</c> is required (R-3). A caller who may not see the
    /// project's tasks as a whole sees the ones they own, where their grant reaches those.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.TaskView)]
    [ProducesResponseType<ProjectTaskPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_ListProjectTasks")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await tasks.ListTasksAsync(CallerId, projectId!.Value, paging, cancellationToken));

    [HttpGet("{taskId:guid}")]
    [RequirePermission(PermissionCatalogue.TaskView)]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_GetProjectTask")]
    public async Task<IActionResult> Get(Guid taskId, CancellationToken cancellationToken) =>
        Respond(await tasks.GetTaskAsync(CallerId, taskId, cancellationToken));

    /// <summary>Adds a task, or a subtask of a top-level task, NOT_STARTED.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.TaskManage)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("ProjectTask_CreateProjectTask")]
    public async Task<IActionResult> Create(ProjectTaskCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ProjectTaskDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await tasks.CreateTaskAsync(CallerId, draft!, cancellationToken), $"/{Collection}", t => t.Id);
    }

    /// <summary>The task's plan and assignment, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{taskId:guid}")]
    [RequirePermission(PermissionCatalogue.TaskManage)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_UpdateProjectTask")]
    public async Task<IActionResult> Update(Guid taskId, ProjectTaskRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ProjectTaskChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await tasks.UpdateTaskAsync(CallerId, taskId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>NOT_STARTED → IN_PROGRESS, once its FS and SS predecessors allow it.</summary>
    [HttpPost("{taskId:guid}/start")]
    [RequirePermission(PermissionCatalogue.TaskUpdate)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_StartProjectTask")]
    public Task<IActionResult> Start(Guid taskId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => execution.StartAsync(CallerId, taskId, version, ct), cancellationToken);

    /// <summary>NOT_STARTED or IN_PROGRESS → BLOCKED, with the reason.</summary>
    [HttpPost("{taskId:guid}/block")]
    [RequirePermission(PermissionCatalogue.TaskUpdate)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_BlockProjectTask")]
    public async Task<IActionResult> Block(Guid taskId, TaskBlockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out NarrativeText? reason) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => execution.BlockAsync(CallerId, taskId, reason!, version, ct), cancellationToken);
    }

    /// <summary>BLOCKED → IN_PROGRESS, or NOT_STARTED for a task blocked before it started.</summary>
    [HttpPost("{taskId:guid}/unblock")]
    [RequirePermission(PermissionCatalogue.TaskUpdate)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_UnblockProjectTask")]
    public Task<IActionResult> Unblock(Guid taskId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => execution.UnblockAsync(CallerId, taskId, version, ct), cancellationToken);

    /// <summary>IN_PROGRESS → COMPLETED, once its FF and SF predecessors and its live subtasks allow it. A BLOCKED task is 409.</summary>
    [HttpPost("{taskId:guid}/complete")]
    [RequirePermission(PermissionCatalogue.TaskUpdate)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_CompleteProjectTask")]
    public Task<IActionResult> Complete(Guid taskId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => execution.CompleteAsync(CallerId, taskId, version, ct), cancellationToken);

    /// <summary>COMPLETED → IN_PROGRESS. TASK_REOPEN only: general edit permission does not reopen a task.</summary>
    [HttpPost("{taskId:guid}/reopen")]
    [RequirePermission(PermissionCatalogue.TaskReopen)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_ReopenProjectTask")]
    public Task<IActionResult> Reopen(Guid taskId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => execution.ReopenAsync(CallerId, taskId, version, ct), cancellationToken);

    /// <summary>A live task → CANCELLED, final: RETAIN has no DELETE (R-5). Not while it has a live subtask or a dependency.</summary>
    [HttpPost("{taskId:guid}/cancel")]
    [RequirePermission(PermissionCatalogue.TaskManage)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_CancelProjectTask")]
    public Task<IActionResult> Cancel(Guid taskId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => execution.CancelAsync(CallerId, taskId, version, ct), cancellationToken);

    /// <summary>A leaf's actual percentage, entered by its owner or the Project Manager while it is IN_PROGRESS or BLOCKED (ADR-009).</summary>
    [HttpPost("{taskId:guid}/report-progress")]
    [RequirePermission(PermissionCatalogue.TaskUpdate)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectTaskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_ReportProjectTaskProgress")]
    public async Task<IActionResult> ReportProgress(Guid taskId, TaskProgressCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out decimal percent) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => execution.ReportProgressAsync(CallerId, taskId, percent, version, ct), cancellationToken);
    }

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<ProjectTaskDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
