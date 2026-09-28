using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Domain.Approval;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-11 approval runs (TASK-035): SCR-101 My Requests, SCR-115 history and MOD-044 withdraw. A run is started by its
/// source module in process, never through this API, and is never edited or deleted.
/// </summary>
[Route(Collection)]
[Tags("Approval")]
public sealed class ApprovalInstancesController(IApprovalWorkflowService approvals) : AdministrationControllerBase
{
    private const string Collection = "api/v1/approval-instances";

    /// <summary>
    /// Either <c>requestedBy=me</c> (SCR-101) or one subject, named by <c>subjectModule</c>, <c>subjectType</c> and
    /// <c>subjectId</c>. Filter: <c>status</c> (a set). Newest request first; only runs the caller may see.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ApprovalView)]
    [ProducesResponseType<ApprovalInstancePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_ListApprovalInstances")]
    public async Task<IActionResult> List(
        [FromQuery] string? requestedBy, [FromQuery] string? subjectModule, [FromQuery] string? subjectType, [FromQuery] Guid? subjectId,
        [FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        bool mine = requestedBy is not null;
        if (mine && requestedBy != "me")
        {
            errors.Add(new FieldError("requestedBy", FieldError.EnumValue));
        }

        bool bySubject = subjectModule is not null || subjectType is not null || subjectId is not null;
        if (mine == bySubject)
        {
            errors.Add(new FieldError(mine ? "subjectId" : "requestedBy", mine ? FieldError.NotAllowed : FieldError.Required));
        }
        else if (bySubject)
        {
            RequestValidation.Require(subjectModule, "subjectModule", 50, errors);
            RequestValidation.Require(subjectType, "subjectType", 100, errors);
            RequestValidation.RequireId(subjectId, "subjectId", errors);
        }

        ApprovalInstanceQuery query = new(
            mine, subjectModule, subjectType, subjectId,
            QueryParameters.EnumSet<ApprovalInstanceStatus>(status, "status", errors), QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await approvals.ListInstancesAsync(CallerId, query, cancellationToken));
    }

    /// <summary>The run and its whole history: every task, including escalated and cancelled ones.</summary>
    [HttpGet("{instanceId:guid}")]
    [RequirePermission(PermissionCatalogue.ApprovalView)]
    [ProducesResponseType<ApprovalInstanceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_GetApprovalInstance")]
    public async Task<IActionResult> Get(Guid instanceId, CancellationToken cancellationToken) =>
        Respond(await approvals.GetInstanceAsync(CallerId, instanceId, cancellationToken));

    /// <summary>By the requester, while PENDING; the outcome WITHDRAWN goes to the source.</summary>
    [HttpPost("{instanceId:guid}/withdraw")]
    [RequirePermission(PermissionCatalogue.ApprovalView)]
    [SensitiveWrite]
    [ProducesResponseType<ApprovalInstanceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_WithdrawApprovalInstance")]
    public async Task<IActionResult> Withdraw(Guid instanceId, CancellationToken cancellationToken) =>
        Respond(await approvals.WithdrawAsync(CallerId, instanceId, cancellationToken));
}
