using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-02 progress submission, review and publication (TASK-044): SCR-048 Project Progress tab, SCR-070 Progress Update
/// History. Each workflow edge is its own command (R-4). Actual and planned progress are derived, never sent (ADR-009).
/// </summary>
[Route(Collection)]
[Tags("Progress")]
public sealed class ProgressSubmissionsController(IProgressService progress) : AdministrationControllerBase
{
    private const string Collection = "api/v1/progress-submissions";

    /// <summary>SCR-070: every revision of the project's periods, newest first. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProgressView)]
    [ProducesResponseType<ProgressSubmissionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_ListProgressSubmissions")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await progress.ListSubmissionsAsync(CallerId, projectId!.Value, paging, cancellationToken));

    [HttpGet("{submissionId:guid}")]
    [RequirePermission(PermissionCatalogue.ProgressView)]
    [ProducesResponseType<ProgressSubmissionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_GetProgressSubmission")]
    public async Task<IActionResult> Get(Guid submissionId, CancellationToken cancellationToken) =>
        Respond(await progress.GetSubmissionAsync(CallerId, submissionId, cancellationToken));

    /// <summary>
    /// Starts the progress update of the project's earliest open period, as a DRAFT with the derived figures, pre-filled from
    /// the last published period (ADR-017).
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ProgressSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<ProgressSubmissionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Progress_StartProgressSubmission")]
    public async Task<IActionResult> Start(ProgressSubmissionStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate() is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await progress.StartAsync(CallerId, request.ProjectId!.Value, cancellationToken), $"/{Collection}", s => s.Id);
    }

    /// <summary>The narrative and the override of a DRAFT, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{submissionId:guid}")]
    [RequirePermission(PermissionCatalogue.ProgressSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<ProgressSubmissionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_UpdateProgressSubmission")]
    public async Task<IActionResult> Update(Guid submissionId, ProgressSubmissionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ProgressSubmissionChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await progress.UpdateAsync(CallerId, submissionId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>DRAFT → SUBMITTED: the derived figures are calculated again and fixed.</summary>
    [HttpPost("{submissionId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.ProgressSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<ProgressSubmissionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_SubmitProgressSubmission")]
    public async Task<IActionResult> Submit(Guid submissionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await progress.SubmitAsync(CallerId, submissionId, version, cancellationToken))
            : problem!;

    /// <summary>SUBMITTED → UNDER_REVIEW, by AHDA; not by the submitter.</summary>
    [HttpPost("{submissionId:guid}/start-review")]
    [RequirePermission(PermissionCatalogue.ProgressReview)]
    [SensitiveWrite]
    [ProducesResponseType<ProgressSubmissionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_StartProgressSubmissionReview")]
    public async Task<IActionResult> StartReview(Guid submissionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await progress.StartReviewAsync(CallerId, submissionId, version, cancellationToken))
            : problem!;

    /// <summary>UNDER_REVIEW → RETURNED with the reason; the period continues as the next revision, a DRAFT.</summary>
    [HttpPost("{submissionId:guid}/return")]
    [RequirePermission(PermissionCatalogue.ProgressReview)]
    [SensitiveWrite]
    [ProducesResponseType<ProgressSubmissionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_ReturnProgressSubmission")]
    public async Task<IActionResult> Return(Guid submissionId, ProgressReturnCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !TryReadIfMatch(required: false, out uint? version, out IActionResult? problem) ? problem!
            : command.Validate(out NarrativeText? reason) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await progress.ReturnAsync(CallerId, submissionId, reason!, version, cancellationToken));
    }

    /// <summary>
    /// UNDER_REVIEW → PUBLISHED, by AHDA: the period's immutable snapshot is written with its Overall Project Health and the
    /// period is closed.
    /// </summary>
    [HttpPost("{submissionId:guid}/publish")]
    [RequirePermission(PermissionCatalogue.ProgressReview)]
    [SensitiveWrite]
    [ProducesResponseType<ProgressSubmissionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_PublishProgressSubmission")]
    public async Task<IActionResult> Publish(Guid submissionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await progress.PublishAsync(CallerId, submissionId, version, cancellationToken))
            : problem!;
}
