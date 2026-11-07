using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Controllers;

/// <summary>Finds the people a field may name (review round 5, item 5). See <see cref="IPersonLookupService"/>.</summary>
[Tags("IdentityAccess")]
public sealed class PersonLookupsController(IPersonLookupService people, IProjectFactsReader projects) : AdministrationControllerBase
{
    /// <summary>
    /// Active people matching <c>q</c>. Filters: <c>userType</c>; <c>externalEntityId</c>; <c>projectId</c>, whose anchors they must hold a role over.
    /// 403 to someone with no write permission.
    /// </summary>
    [HttpGet("api/v1/person-lookups")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<PersonMatchPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListPersonLookups")]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] UserType? userType,
        [FromQuery] Guid? externalEntityId,
        [FromQuery] Guid? projectId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest request = QueryParameters.Page(page, pageSize, errors);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        RoleHolderScope? scope = null;
        if (projectId is { } id)
        {
            if (await projects.FindAsync(id, cancellationToken) is not { } project)
            {
                return ValidationFailed([new FieldError("projectId", FieldError.Malformed)]);
            }

            scope = new RoleHolderScope(project.Id, project.DepartmentId, project.ExternalEntityId);
        }

        return await people.SearchAsync(CallerId, new PersonLookupQuery(q, userType, externalEntityId, scope, request), cancellationToken) is { } found
            ? Ok(found)
            : Failure(AdministrationError.Forbidden);
    }
}
