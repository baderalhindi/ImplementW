using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ChangeRequest;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-08 (TASK-060): the change authorisations approval issued — scoped to one project, one kind of change and one target version — and
/// their application by the target module. Read-only here: approval issues them, and a target module applies one through WF-08's typed
/// adapter when it makes the change (a rebaseline names one in <c>POST /project-baselines/{id}/submit</c>, an Approved Budget change in
/// <c>POST /financial-commitments/{id}/submit</c>).
/// </summary>
[Route(Collection)]
[Tags("ChangeRequest")]
public sealed class ChangeAuthorizationsController(IChangeAuthorizationService authorizations) : AdministrationControllerBase
{
    private const string Collection = "api/v1/change-authorizations";

    /// <summary>
    /// The project's change authorisations, most recently issued first. <c>projectId</c> is required (R-3). Filters: <c>changeRequestId</c>,
    /// <c>authorizationScope</c> and <c>status</c> (sets) — what a target module's screen offers, e.g. the ISSUED REBASELINE ones.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ChangeRequestView)]
    [ProducesResponseType<ChangeAuthorizationPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_ListChangeAuthorizations")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId, [FromQuery] Guid? changeRequestId, [FromQuery] string? authorizationScope, [FromQuery] string? status, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        IReadOnlyCollection<ChangeAuthorizationScope> scopes = QueryParameters.EnumSet<ChangeAuthorizationScope>(authorizationScope, "authorizationScope", errors);
        IReadOnlyCollection<ChangeAuthorizationStatus> statuses = QueryParameters.EnumSet<ChangeAuthorizationStatus>(status, "status", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await authorizations.ListAsync(CallerId, new ChangeAuthorizationQuery(projectId!.Value, changeRequestId, scopes, statuses), paging, cancellationToken));
    }

    [HttpGet("{changeAuthorizationId:guid}")]
    [RequirePermission(PermissionCatalogue.ChangeRequestView)]
    [ProducesResponseType<ChangeAuthorizationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ChangeRequest_GetChangeAuthorization")]
    public async Task<IActionResult> Get(Guid changeAuthorizationId, CancellationToken cancellationToken) =>
        Respond(await authorizations.GetAsync(CallerId, changeAuthorizationId, cancellationToken));
}
