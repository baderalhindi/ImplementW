using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Suspension;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-09 (TASK-062): a project's suspension and resumption requests — SCR-108 to SCR-110 — and a request's way along its state machine.
/// Each command is one edge (R-4) and answers with the request. Approval is WF-11's decision, on the approval endpoints, and changes no
/// project; <c>activate</c> is the separate lifecycle transition, which WF-09 also runs itself on the effective date.
/// </summary>
[Route(Collection)]
[Tags("Suspension")]
public sealed class SuspensionRequestsController(ISuspensionRequestService requests, ISuspensionLifecycleService lifecycle) : AdministrationControllerBase
{
    private const string Collection = "api/v1/suspension-requests";

    /// <summary>The project's requests, most recently changed first. <c>projectId</c> is required (R-3). Filters: <c>requestType</c> and <c>status</c> (sets).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.SuspensionView)]
    [ProducesResponseType<SuspensionRequestPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Suspension_ListSuspensionRequests")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId, [FromQuery] string? requestType, [FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        IReadOnlyCollection<SuspensionRequestType> types = QueryParameters.EnumSet<SuspensionRequestType>(requestType, "requestType", errors);
        IReadOnlyCollection<SuspensionRequestStatus> statuses = QueryParameters.EnumSet<SuspensionRequestStatus>(status, "status", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await requests.ListAsync(CallerId, new SuspensionRequestQuery(projectId!.Value, types, statuses), paging, cancellationToken));
    }

    [HttpGet("{suspensionRequestId:guid}")]
    [RequirePermission(PermissionCatalogue.SuspensionView)]
    [ProducesResponseType<SuspensionRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Suspension_GetSuspensionRequest")]
    public async Task<IActionResult> Get(Guid suspensionRequestId, CancellationToken cancellationToken) =>
        Respond(await requests.GetAsync(CallerId, suspensionRequestId, cancellationToken));

    /// <summary>
    /// Raises a request (SCR-109), DRAFT: SUSPEND for an ACTIVE project, RESUME for a SUSPENDED one. A project already suspended, or with a
    /// request of the type open, is refused 409. An entity Project Manager raises on their own project (ADR-013).
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.SuspensionRaise)]
    [SensitiveWrite]
    [ProducesResponseType<SuspensionRequestDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Suspension_CreateSuspensionRequest")]
    public async Task<IActionResult> Create(SuspensionRequestCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out SuspensionRequestDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await requests.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", r => r.Id);
    }

    /// <summary>A DRAFT or RETURNED request's own fields, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{suspensionRequestId:guid}")]
    [RequirePermission(PermissionCatalogue.SuspensionRaise)]
    [SensitiveWrite]
    [ProducesResponseType<SuspensionRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Suspension_UpdateSuspensionRequest")]
    public async Task<IActionResult> Update(Guid suspensionRequestId, SuspensionRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out SuspensionRequestChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await requests.UpdateAsync(CallerId, suspensionRequestId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>HARD_DRAFT: a DRAFT never submitted. R-40: a request that is not there — deleted already, or never the caller's to see — is 204 as well.</summary>
    [HttpDelete("{suspensionRequestId:guid}")]
    [RequirePermission(PermissionCatalogue.SuspensionRaise)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("Suspension_DeleteSuspensionRequest")]
    public async Task<IActionResult> Delete(Guid suspensionRequestId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await requests.DeleteAsync(CallerId, suspensionRequestId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>DRAFT or RETURNED → SUBMITTED, with an effective date not before today; a RETURNED request comes back as the next revision.</summary>
    [HttpPost("{suspensionRequestId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.SuspensionRaise)]
    [SensitiveWrite]
    [ProducesResponseType<SuspensionRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Suspension_Submit")]
    public Task<IActionResult> Submit(Guid suspensionRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.SubmitAsync(CallerId, suspensionRequestId, version, ct), cancellationToken);

    /// <summary>SUBMITTED or RETURNED → WITHDRAWN. Under review, the requester withdraws the WF-11 run instead, which withdraws the request.</summary>
    [HttpPost("{suspensionRequestId:guid}/withdraw")]
    [RequirePermission(PermissionCatalogue.SuspensionRaise)]
    [SensitiveWrite]
    [ProducesResponseType<SuspensionRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Suspension_Withdraw")]
    public Task<IActionResult> Withdraw(Guid suspensionRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.WithdrawAsync(CallerId, suspensionRequestId, version, ct), cancellationToken);

    /// <summary>SUBMITTED → UNDER_REVIEW: starts the WF-11 run of the SUSPENSION or RESUMPTION route. Internal users only.</summary>
    [HttpPost("{suspensionRequestId:guid}/start-review")]
    [RequirePermission(PermissionCatalogue.SuspensionReview)]
    [SensitiveWrite]
    [ProducesResponseType<SuspensionRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Suspension_StartReview")]
    public Task<IActionResult> StartReview(Guid suspensionRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.StartReviewAsync(CallerId, suspensionRequestId, version, ct), cancellationToken);

    /// <summary>
    /// APPROVED → EFFECTED on or after its effective date: the project's lifecycle transition, ACTIVE → SUSPENDED or SUSPENDED → ACTIVE,
    /// with its active suspension opened or ended, in one transaction. No baseline changes. Safe to retry. Internal users only.
    /// </summary>
    [HttpPost("{suspensionRequestId:guid}/activate")]
    [RequirePermission(PermissionCatalogue.SuspensionActivate)]
    [SensitiveWrite]
    [ProducesResponseType<SuspensionRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Suspension_Activate")]
    public Task<IActionResult> Activate(Guid suspensionRequestId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.ActivateAsync(CallerId, suspensionRequestId, version, ct), cancellationToken);

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<SuspensionRequestDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
