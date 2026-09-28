using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.Approval;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-11 approval tasks (TASK-035): SCR-100 Approval Inbox and MOD-040–042, MOD-045. Each decision re-checks the
/// caller's authority over the task at that moment, by their own grants or a delegator's; a retried decision on a task
/// already decided answers 409 <c>TERMINAL_STATE</c> and changes nothing.
/// </summary>
[Route(Collection)]
[Tags("Approval")]
public sealed class ApprovalTasksController(IApprovalWorkflowService approvals) : AdministrationControllerBase
{
    private const string Collection = "api/v1/approval-tasks";

    /// <summary>The tasks the caller may decide now, earliest due first.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ApprovalDecide)]
    [ProducesResponseType<ApprovalInboxPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_ListApprovalTasks")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest request = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await approvals.ListInboxAsync(CallerId, request, cancellationToken));
    }

    [HttpPost("{taskId:guid}/approve")]
    [RequirePermission(PermissionCatalogue.ApprovalDecide)]
    [SensitiveWrite]
    [ProducesResponseType<ApprovalInstanceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_ApproveApprovalTask")]
    public Task<IActionResult> Approve(
        Guid taskId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApprovalDecisionCommand? command, CancellationToken cancellationToken) =>
        DecideAsync(taskId, ApprovalTaskDecision.Approve, command, cancellationToken);

    /// <summary>Ends the run REJECTED. <c>reason</c> is required.</summary>
    [HttpPost("{taskId:guid}/reject")]
    [RequirePermission(PermissionCatalogue.ApprovalDecide)]
    [SensitiveWrite]
    [ProducesResponseType<ApprovalInstanceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_RejectApprovalTask")]
    public Task<IActionResult> Reject(
        Guid taskId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApprovalDecisionCommand? command, CancellationToken cancellationToken) =>
        DecideAsync(taskId, ApprovalTaskDecision.Reject, command, cancellationToken);

    /// <summary>Ends the run RETURNED; the source resubmits a new revision, which starts a new run. <c>reason</c> is required.</summary>
    [HttpPost("{taskId:guid}/return")]
    [RequirePermission(PermissionCatalogue.ApprovalDecide)]
    [SensitiveWrite]
    [ProducesResponseType<ApprovalInstanceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_ReturnApprovalTask")]
    public Task<IActionResult> Return(
        Guid taskId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApprovalDecisionCommand? command, CancellationToken cancellationToken) =>
        DecideAsync(taskId, ApprovalTaskDecision.Return, command, cancellationToken);

    /// <summary>By the requester, once the task is overdue: it passes to WORKFLOW_POLICY's escalation role.</summary>
    [HttpPost("{taskId:guid}/escalate")]
    [RequirePermission(PermissionCatalogue.ApprovalView)]
    [SensitiveWrite]
    [ProducesResponseType<ApprovalInstanceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_EscalateApprovalTask")]
    public async Task<IActionResult> Escalate(
        Guid taskId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApprovalDecisionCommand? command, CancellationToken cancellationToken)
    {
        NarrativeText? reason = null;
        return command?.Validate(reasonRequired: false, out reason) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await approvals.EscalateAsync(CallerId, taskId, reason, cancellationToken));
    }

    private async Task<IActionResult> DecideAsync(Guid taskId, ApprovalTaskDecision decision, ApprovalDecisionCommand? command, CancellationToken cancellationToken)
    {
        List<FieldError> errors = (command ?? new ApprovalDecisionCommand(null)).Validate(decision != ApprovalTaskDecision.Approve, out NarrativeText? reason);
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await approvals.DecideAsync(CallerId, taskId, decision, reason, cancellationToken));
    }
}
