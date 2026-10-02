using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Progress.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-02 PUBLISHED/OFFICIAL progress (TASK-044): each period's immutable snapshot with the Overall Project Health WF-02
/// computed at publication (ICD-03). Read-only: a snapshot is written by publishing and never changed.
/// </summary>
[Route("api/v1/published-progress-snapshots")]
[Tags("Progress")]
public sealed class PublishedProgressSnapshotsController(IProgressService progress) : AdministrationControllerBase
{
    /// <summary>The project's snapshots, latest first. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProgressView)]
    [ProducesResponseType<PublishedProgressSnapshotPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_ListPublishedProgressSnapshots")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await progress.ListSnapshotsAsync(CallerId, projectId!.Value, paging, cancellationToken));
}
