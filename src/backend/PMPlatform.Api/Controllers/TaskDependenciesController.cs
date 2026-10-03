using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Api.Models.ProjectTask;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ProjectTask.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>WF-04 (TASK-048): the blocking dependencies between a project's tasks.</summary>
[Route(Collection)]
[Tags("ProjectTask")]
public sealed class TaskDependenciesController(ITaskDependencyService dependencies) : AdministrationControllerBase
{
    private const string Collection = "api/v1/task-dependencies";

    /// <summary>The project's task dependencies, oldest first. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.TaskView)]
    [ProducesResponseType<TaskDependencyPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_ListTaskDependencies")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await dependencies.ListDependenciesAsync(CallerId, projectId!.Value, paging, cancellationToken));

    /// <summary>Links two live leaf tasks of one project; one that would close a cycle, or is already broken, is refused.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.TaskManage)]
    [SensitiveWrite]
    [ProducesResponseType<TaskDependencyDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("ProjectTask_CreateTaskDependency")]
    public async Task<IActionResult> Create(TaskDependencyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out TaskDependencyDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await dependencies.CreateDependencyAsync(CallerId, draft!, cancellationToken), $"/{Collection}", d => d.Id);
    }

    /// <summary>HARD_WORKING. R-40: a dependency that is not there — removed already, or never the caller's to see — is 204 as well.</summary>
    [HttpDelete("{dependencyId:guid}")]
    [RequirePermission(PermissionCatalogue.TaskManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("ProjectTask_DeleteTaskDependency")]
    public async Task<IActionResult> Delete(Guid dependencyId, CancellationToken cancellationToken)
    {
        AdministrationError? error = await dependencies.DeleteDependencyAsync(CallerId, dependencyId, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }
}
