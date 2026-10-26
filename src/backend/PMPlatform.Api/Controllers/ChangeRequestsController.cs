using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.ChangeRequest;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ChangeRequest;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-08 (TASK-060): a project's change requests — SCR-105 to SCR-107 — and a request's way along its state machine. Each command is one
/// edge (R-4) and answers with the request. Materiality is the server's: previewed before review, recorded and pinned when AHDA starts
/// the review. Approval is WF-11's decision, on the approval endpoints; it issues change authorisations and changes no other module.
/// </summary>
[Route(Collection)]
[Tags("ChangeRequest")]
public sealed class ChangeRequestsController(IChangeRequestService requests, IChangeRequestLifecycleService lifecycle) : AdministrationControllerBase
{
    private const string Collection = "api/v1/change-requests";

    /// <summary>The project's change requests, most recently changed first. <c>projectId</c> is required (R-3). Filters: <c>status</c> and <c>changeType</c> (sets).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ChangeRequestView)]
    [ProducesResponseType<ChangeRequestPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_ListChangeRequests")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId, [FromQuery] string? status, [FromQuery] string? changeType, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        IReadOnlyCollection<ChangeRequestStatus> statuses = QueryParameters.EnumSet<ChangeRequestStatus>(status, "status", errors);
        IReadOnlyCollection<ChangeType> types = QueryParameters.EnumSet<ChangeType>(changeType, "changeType", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await requests.ListAsync(CallerId, new ChangeRequestQuery(projectId!.Value, statuses, types), paging, cancellationToken));
    }

    [HttpGet("{changeRequestId:guid}")]
    [RequirePermission(PermissionCatalogue.ChangeRequestView)]
    [ProducesResponseType<ChangeRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_GetChangeRequest")]
    public async Task<IActionResult> Get(Guid changeRequestId, CancellationToken cancellationToken) =>
        Respond(await requests.GetAsync(CallerId, changeRequestId, cancellationToken));

    /// <summary>Raises a change request (SCR-106), DRAFT. An entity Project Manager raises on their own project (ADR-013).</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ChangeRequestRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ChangeRequestDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("ChangeRequest_CreateChangeRequest")]
    public async Task<IActionResult> Create(ChangeRequestCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ChangeRequestDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await requests.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", r => r.Id);
    }

    /// <summary>A DRAFT or RETURNED request's own fields, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{changeRequestId:guid}")]
    [RequirePermission(PermissionCatalogue.ChangeRequestRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ChangeRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_UpdateChangeRequest")]
    public async Task<IActionResult> Update(Guid changeRequestId, ChangeRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ChangeRequestChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await requests.UpdateAsync(CallerId, changeRequestId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>HARD_DRAFT: a DRAFT never submitted. R-40: a request that is not there — deleted already, or never the caller's to see — is 204 as well.</summary>
    [HttpDelete("{changeRequestId:guid}")]
    [RequirePermission(PermissionCatalogue.ChangeRequestRaise)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("ChangeRequest_DeleteChangeRequest")]
    public async Task<IActionResult> Delete(Guid changeRequestId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await requests.DeleteAsync(CallerId, changeRequestId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>
    /// The materiality classification the request would get now (SCR-106), by the rule its review applies: advisory, recorded nowhere, so
    /// the requester sees the approval path before submitting. Before the review only; after it, the recorded one is on the request.
    /// </summary>
    [HttpPost("{changeRequestId:guid}/preview-materiality")]
    [RequirePermission(PermissionCatalogue.ChangeRequestView)]
    [SensitiveWrite]
    [ProducesResponseType<MaterialityAssessment>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_PreviewMateriality")]
    public async Task<IActionResult> PreviewMateriality(Guid changeRequestId, CancellationToken cancellationToken) =>
        Respond(await requests.PreviewMaterialityAsync(CallerId, changeRequestId, cancellationToken));

    /// <summary>DRAFT or RETURNED → SUBMITTED, complete for its change type; a RETURNED request comes back as the next revision.</summary>
    [HttpPost("{changeRequestId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.ChangeRequestRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ChangeRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_Submit")]
    public Task<IActionResult> Submit(Guid changeRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.SubmitAsync(CallerId, changeRequestId, version, ct), cancellationToken);

    /// <summary>SUBMITTED or RETURNED → WITHDRAWN. Under review, the requester withdraws the WF-11 run instead, which withdraws the request.</summary>
    [HttpPost("{changeRequestId:guid}/withdraw")]
    [RequirePermission(PermissionCatalogue.ChangeRequestRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ChangeRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_Withdraw")]
    public Task<IActionResult> Withdraw(Guid changeRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.WithdrawAsync(CallerId, changeRequestId, version, ct), cancellationToken);

    /// <summary>
    /// SUBMITTED → UNDER_REVIEW: records the materiality evaluation, pinned, and starts the WF-11 run its band routes. Internal users only.
    /// </summary>
    [HttpPost("{changeRequestId:guid}/start-review")]
    [RequirePermission(PermissionCatalogue.ChangeRequestReview)]
    [SensitiveWrite]
    [ProducesResponseType<ChangeRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_StartReview")]
    public Task<IActionResult> StartReview(Guid changeRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.StartReviewAsync(CallerId, changeRequestId, version, ct), cancellationToken);

    /// <summary>APPROVED → IMPLEMENTATION: from now on its target modules may apply its authorisations. Internal users only.</summary>
    [HttpPost("{changeRequestId:guid}/start-implementation")]
    [RequirePermission(PermissionCatalogue.ChangeRequestImplement)]
    [SensitiveWrite]
    [ProducesResponseType<ChangeRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_StartImplementation")]
    public Task<IActionResult> StartImplementation(Guid changeRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.StartImplementationAsync(CallerId, changeRequestId, version, ct), cancellationToken);

    /// <summary>IMPLEMENTATION → IMPLEMENTED, once every authorisation has been applied by its target module. Internal users only.</summary>
    [HttpPost("{changeRequestId:guid}/mark-implemented")]
    [RequirePermission(PermissionCatalogue.ChangeRequestImplement)]
    [SensitiveWrite]
    [ProducesResponseType<ChangeRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_MarkImplemented")]
    public Task<IActionResult> MarkImplemented(Guid changeRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.MarkImplementedAsync(CallerId, changeRequestId, version, ct), cancellationToken);

    /// <summary>IMPLEMENTED → CLOSED. Internal users only.</summary>
    [HttpPost("{changeRequestId:guid}/close")]
    [RequirePermission(PermissionCatalogue.ChangeRequestImplement)]
    [SensitiveWrite]
    [ProducesResponseType<ChangeRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_Close")]
    public Task<IActionResult> Close(Guid changeRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.CloseAsync(CallerId, changeRequestId, version, ct), cancellationToken);

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<ChangeRequestDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
