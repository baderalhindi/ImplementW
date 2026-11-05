using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>The 5 Nov 2026 review §2.2: monthly update requests sent to one, several or all projects as one batch, and the batch's page.</summary>
[Route(Collection)]
[Tags("ExternalParticipation")]
public sealed class UpdateRequestBatchesController(IUpdateRequestBatchService batches) : AdministrationControllerBase
{
    private const string Collection = "api/v1/update-request-batches";

    [HttpGet]
    [RequirePermission(PermissionCatalogue.ExternalRequestView)]
    [ProducesResponseType<UpdateRequestBatchPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_ListUpdateRequestBatches")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest request = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await batches.ListAsync(CallerId, request, cancellationToken));
    }

    [HttpGet("{batchId:guid}")]
    [RequirePermission(PermissionCatalogue.ExternalRequestView)]
    [ProducesResponseType<UpdateRequestBatchDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_GetUpdateRequestBatch")]
    public async Task<IActionResult> Get(Guid batchId, CancellationToken cancellationToken) =>
        Respond(await batches.GetAsync(CallerId, batchId, cancellationToken));

    /// <summary>All or nothing: every project that cannot be sent to is named in the 422.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.UpdateRequestSend)]
    [SensitiveWrite]
    [ProducesResponseType<UpdateRequestBatchDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("ExternalParticipation_SendUpdateRequestBatch")]
    public async Task<IActionResult> Send(UpdateRequestBatchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate(out UpdateRequestBatchDraft? draft) is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AdministrationResult<UpdateRequestBatchDetail> sent = await batches.SendAsync(CallerId, draft!, cancellationToken);
        return sent.Succeeded ? Created($"/{Collection}/{sent.Value!.Batch.Id:D}", sent.Value) : Respond(sent);
    }

    [HttpPost("{batchId:guid}/remind-late")]
    [RequirePermission(PermissionCatalogue.UpdateRequestSend)]
    [SensitiveWrite]
    [ProducesResponseType<UpdateRequestBatchDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_RemindLateUpdateRequests")]
    public async Task<IActionResult> RemindLate(Guid batchId, CancellationToken cancellationToken) =>
        Respond(await batches.RemindLateAsync(CallerId, batchId, cancellationToken));

    [HttpPost("{batchId:guid}/withdraw")]
    [RequirePermission(PermissionCatalogue.UpdateRequestSend)]
    [SensitiveWrite]
    [ProducesResponseType<UpdateRequestBatchDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_WithdrawUpdateRequestBatch")]
    public async Task<IActionResult> Withdraw(Guid batchId, UpdateRequestBatchWithdrawRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out NarrativeText? reason) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await batches.WithdrawAsync(CallerId, batchId, reason!, cancellationToken));
    }
}

/// <summary>The 5 Nov 2026 review §2.1: the monthly updates waiting on the signed-in person, for the window and the bell.</summary>
[Route("api/v1/my-update-requests")]
[Tags("ExternalParticipation")]
public sealed class MyUpdateRequestsController(IMyUpdateRequests requests) : AdministrationControllerBase
{
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<MyUpdateRequestPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_ListMyUpdateRequests")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest request = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await requests.ListAsync(CallerId, request, cancellationToken));
    }

    /// <summary>Records that the responder opened this project's form: the batch page's "opened".</summary>
    [HttpPost("{requestId:guid}/mark-opened")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("ExternalParticipation_MarkUpdateRequestOpened")]
    public async Task<IActionResult> MarkOpened(Guid requestId, CancellationToken cancellationToken) =>
        await requests.MarkOpenedAsync(CallerId, requestId, cancellationToken) is { } error
            ? Respond((AdministrationResult<UpdateRequestBatchDetail>)error)
            : NoContent();
}
