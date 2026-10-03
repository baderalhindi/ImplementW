using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Api.Models.Schedule;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>WF-03 (TASK-046): the dependency network of a project's schedule, acyclic by rule.</summary>
[Route(Collection)]
[Tags("Schedule")]
public sealed class ScheduleDependenciesController(IScheduleService schedules) : AdministrationControllerBase
{
    private const string Collection = "api/v1/schedule-dependencies";

    /// <summary>The project's dependencies, oldest first. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<ScheduleDependencyPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_ListScheduleDependencies")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await schedules.ListDependenciesAsync(CallerId, projectId!.Value, paging, cancellationToken));

    /// <summary>Adds a dependency between two leaf activities; one that would close a cycle is refused (VAL-SCH-008).</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ScheduleDependencyDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Schedule_CreateScheduleDependency")]
    public async Task<IActionResult> Create(ScheduleDependencyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ScheduleDependencyDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await schedules.CreateDependencyAsync(CallerId, draft!, cancellationToken), $"/{Collection}", d => d.Id);
    }

    /// <summary>HARD_WORKING. R-40: a dependency that is not there — removed already, or never the caller's to see — is 204 as well.</summary>
    [HttpDelete("{dependencyId:guid}")]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("Schedule_DeleteScheduleDependency")]
    public async Task<IActionResult> Delete(Guid dependencyId, CancellationToken cancellationToken)
    {
        AdministrationError? error = await schedules.DeleteDependencyAsync(CallerId, dependencyId, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }
}
