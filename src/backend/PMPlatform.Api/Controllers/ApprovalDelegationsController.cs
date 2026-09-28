using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Approval;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-11 delegations (TASK-035): SCR-114 and MOD-043. A delegation conveys the delegator's own authority as it stands
/// when the delegate acts, and nothing the delegator holds only through a delegation. Never edited or deleted; revoked.
/// </summary>
[Route(Collection)]
[Tags("Approval")]
public sealed class ApprovalDelegationsController(IApprovalDelegationService delegations) : AdministrationControllerBase
{
    private const string Collection = "api/v1/approval-delegations";

    /// <summary>The delegations the caller gave and those given to them.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ApprovalDecide)]
    [ProducesResponseType<ApprovalDelegationList>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_ListApprovalDelegations")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await delegations.ListAsync(CallerId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCatalogue.ApprovalDecide)]
    [SensitiveWrite]
    [ProducesResponseType<ApprovalDelegationDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Approval_CreateApprovalDelegation")]
    public async Task<IActionResult> Create(ApprovalDelegationCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate(out ApprovalDelegationDraft? draft) is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AdministrationResult<ApprovalDelegationDetail> result = await delegations.CreateAsync(CallerId, draft!, cancellationToken);
        return result.Succeeded ? Created($"/{Collection}/{result.Value.Id}", result.Value) : Failure(result.Error);
    }

    /// <summary>By the delegator; ACTIVE → REVOKED.</summary>
    [HttpPost("{delegationId:guid}/revoke")]
    [RequirePermission(PermissionCatalogue.ApprovalDecide)]
    [SensitiveWrite]
    [ProducesResponseType<ApprovalDelegationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Approval_RevokeApprovalDelegation")]
    public async Task<IActionResult> Revoke(Guid delegationId, CancellationToken cancellationToken) =>
        Respond(await delegations.RevokeAsync(CallerId, delegationId, cancellationToken));
}
