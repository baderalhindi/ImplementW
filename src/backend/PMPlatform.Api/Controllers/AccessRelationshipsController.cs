using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-010 Role Assignment and MOD-081 (TASK-031): a user bound to a published profile version (ADR-018). An assignment
/// is never deleted or edited; <c>end</c> ends it. The user's next request is decided on the change.
/// </summary>
[Route(Collection)]
[Tags("IdentityAccess")]
public sealed class AccessRelationshipsController(IAccessRelationshipService assignments) : IdentityAccessControllerBase
{
    private const string Collection = "api/v1/access-relationships";

    /// <summary>Filters: <c>userId</c>, <c>projectId</c>, <c>status</c> (set). Newest start first.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.UserView)]
    [ProducesResponseType<AccessRelationshipPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListAccessRelationships")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? userId, [FromQuery] Guid? projectId, [FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        AccessRelationshipQuery query = new(
            userId, projectId, QueryParameters.EnumSet<AccessRelationshipStatus>(status, "status", errors), QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await assignments.ListAsync(CallerId, query, cancellationToken));
    }

    [HttpGet("{accessRelationshipId:guid}")]
    [RequirePermission(PermissionCatalogue.UserView)]
    [ProducesResponseType<AccessRelationshipDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetAccessRelationship")]
    public async Task<IActionResult> Get(Guid accessRelationshipId, CancellationToken cancellationToken) =>
        Respond(await assignments.GetAsync(CallerId, accessRelationshipId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCatalogue.RoleAssign)]
    [SensitiveWrite]
    [ProducesResponseType<AccessRelationshipDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("IdentityAccess_CreateAccessRelationship")]
    public async Task<IActionResult> Create(AccessRelationshipCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate(out AccessRelationshipDraft? draft) is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AdministrationResult<AccessRelationshipDetail> result = await assignments.CreateAsync(CallerId, draft!, cancellationToken);
        return result.Succeeded ? Created($"/{Collection}/{result.Value.Id}", result.Value) : Failure(result.Error);
    }

    /// <summary>ACTIVE → ENDED, reason MANUAL.</summary>
    [HttpPost("{accessRelationshipId:guid}/end")]
    [RequirePermission(PermissionCatalogue.RoleAssign)]
    [SensitiveWrite]
    [ProducesResponseType<AccessRelationshipDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_EndAccessRelationship")]
    public async Task<IActionResult> End(Guid accessRelationshipId, CancellationToken cancellationToken) =>
        Respond(await assignments.EndAsync(CallerId, accessRelationshipId, cancellationToken));
}
