using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.ExternalParticipation;
using PMPlatform.Api.Models.FinancialKpi;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-13 (TASK-066): AHDA's update requests to an external entity — SCR-160 to SCR-163. AHDA drafts, issues and cancels them and names the
/// responder and reviewer; an external caller lists and reads only its own entity's issued requests, as the least-disclosure projection,
/// with every internal-only field omitted and named in <c>maskedFields</c> (R-20). A request outside the caller's data scope is 404 (R-47).
/// </summary>
[Route(Collection)]
[Tags("ExternalParticipation")]
[OmitMaskedFields]
public sealed class ExternalUpdateRequestsController(IExternalUpdateRequestService requests) : AdministrationControllerBase
{
    private const string Collection = "api/v1/external-update-requests";

    /// <summary>
    /// The requests the caller's data scope reaches, most recently changed first: AHDA's within its scope; an external caller's own entity's,
    /// issued ones only. Filters: <c>projectId</c>, <c>externalEntityId</c>, <c>status</c> (a set), <c>responsibleUserId</c>, <c>reviewerUserId</c>.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ExternalRequestView)]
    [ProducesResponseType<ExternalUpdateRequestPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_ListExternalUpdateRequests")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId, [FromQuery] Guid? externalEntityId, [FromQuery] string? status, [FromQuery] Guid? responsibleUserId,
        [FromQuery] Guid? reviewerUserId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        IReadOnlyCollection<ExternalUpdateRequestStatus> statuses = QueryParameters.EnumSet<ExternalUpdateRequestStatus>(status, "status", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await requests.ListAsync(CallerId, new ExternalUpdateRequestQuery(projectId, externalEntityId, statuses, responsibleUserId, reviewerUserId), paging, cancellationToken));
    }

    [HttpGet("{externalUpdateRequestId:guid}")]
    [RequirePermission(PermissionCatalogue.ExternalRequestView)]
    [ProducesResponseType<ExternalUpdateRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_GetExternalUpdateRequest")]
    public async Task<IActionResult> Get(Guid externalUpdateRequestId, CancellationToken cancellationToken) =>
        Respond(await requests.GetAsync(CallerId, externalUpdateRequestId, cancellationToken));

    /// <summary>Drafts a request (SCR-161), invisible to the entity until it is issued. AHDA only.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ExternalRequestManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalUpdateRequestDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("ExternalParticipation_CreateExternalUpdateRequest")]
    public async Task<IActionResult> Create(ExternalUpdateRequestCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ExternalUpdateRequestDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await requests.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", r => r.Id);
    }

    /// <summary>A DRAFT request's own fields, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{externalUpdateRequestId:guid}")]
    [RequirePermission(PermissionCatalogue.ExternalRequestManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalUpdateRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_UpdateExternalUpdateRequest")]
    public async Task<IActionResult> Update(Guid externalUpdateRequestId, ExternalUpdateRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ExternalUpdateRequestChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await requests.UpdateAsync(CallerId, externalUpdateRequestId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>HARD_DRAFT: a DRAFT never issued. R-40: a request that is not there — deleted already, or never the caller's to see — is 204 as well.</summary>
    [HttpDelete("{externalUpdateRequestId:guid}")]
    [RequirePermission(PermissionCatalogue.ExternalRequestManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("ExternalParticipation_DeleteExternalUpdateRequest")]
    public async Task<IActionResult> Delete(Guid externalUpdateRequestId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await requests.DeleteAsync(CallerId, externalUpdateRequestId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>
    /// DRAFT → ISSUED: the project admits it, the entity is active, the contribution type has its typed schema and is enabled for the project's
    /// participation mode, the source record is the project's, the responder and reviewer are eligible, the due date is not past.
    /// </summary>
    [HttpPost("{externalUpdateRequestId:guid}/issue")]
    [RequirePermission(PermissionCatalogue.ExternalRequestManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalUpdateRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_Issue")]
    public Task<IActionResult> Issue(Guid externalUpdateRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => requests.IssueAsync(CallerId, externalUpdateRequestId, version, ct), cancellationToken);

    /// <summary>ISSUED or IN_PROGRESS → CANCELLED, with why. A request whose answer is with AHDA's reviewer is decided instead.</summary>
    [HttpPost("{externalUpdateRequestId:guid}/cancel")]
    [RequirePermission(PermissionCatalogue.ExternalRequestManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalUpdateRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_Cancel")]
    public Task<IActionResult> Cancel(Guid externalUpdateRequestId, ExternalUpdateRequestCancelCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out NarrativeText? reason) is { Count: > 0 } errors
            ? Task.FromResult(ValidationFailed(errors) as IActionResult)
            : CommandAsync((version, ct) => requests.CancelAsync(CallerId, externalUpdateRequestId, reason!, version, ct), cancellationToken);
    }

    /// <summary>Names another responder of the entity on an issued request (WF-13 §9.4); earlier revisions keep their contributor.</summary>
    [HttpPost("{externalUpdateRequestId:guid}/assign-responder")]
    [RequirePermission(PermissionCatalogue.ExternalRequestManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalUpdateRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_AssignResponder")]
    public Task<IActionResult> AssignResponder(Guid externalUpdateRequestId, ResponderAssignCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate() is { Count: > 0 } errors
            ? Task.FromResult(ValidationFailed(errors) as IActionResult)
            : CommandAsync((version, ct) => requests.AssignResponderAsync(CallerId, externalUpdateRequestId, request.ResponsibleUserId!.Value, version, ct), cancellationToken);
    }

    /// <summary>Names another AHDA reviewer on an issued request.</summary>
    [HttpPost("{externalUpdateRequestId:guid}/assign-reviewer")]
    [RequirePermission(PermissionCatalogue.ExternalRequestManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalUpdateRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_AssignReviewer")]
    public Task<IActionResult> AssignReviewer(Guid externalUpdateRequestId, ReviewerAssignCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate() is { Count: > 0 } errors
            ? Task.FromResult(ValidationFailed(errors) as IActionResult)
            : CommandAsync((version, ct) => requests.AssignReviewerAsync(CallerId, externalUpdateRequestId, request.ReviewerUserId!.Value, version, ct), cancellationToken);
    }

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
