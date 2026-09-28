using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>ADM-011 Organization Structure and ADM-012 Departments (TASK-031). Never deleted (RETAIN): deactivated.</summary>
[Route(Collection)]
[Tags("IdentityAccess")]
public sealed class DepartmentsController(IDepartmentAdministrationService departments) : AdministrationControllerBase
{
    private const string Collection = "api/v1/departments";

    /// <summary>Filters: <c>isActive</c>, <c>parentDepartmentId</c> (one level of the tree), <c>q</c>. By code.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.OrganizationView)]
    [ProducesResponseType<DepartmentPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListDepartments")]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive, [FromQuery] Guid? parentDepartmentId, [FromQuery] string? q, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        DepartmentQuery query = new(isActive, parentDepartmentId, q, QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await departments.ListAsync(CallerId, query, cancellationToken));
    }

    [HttpGet("{departmentId:guid}")]
    [RequirePermission(PermissionCatalogue.OrganizationView)]
    [ProducesResponseType<DepartmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetDepartment")]
    public async Task<IActionResult> Get(Guid departmentId, CancellationToken cancellationToken) =>
        Respond(await departments.GetAsync(CallerId, departmentId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCatalogue.OrganizationManage)]
    [SensitiveWrite]
    [ProducesResponseType<DepartmentDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("IdentityAccess_CreateDepartment")]
    public async Task<IActionResult> Create(DepartmentCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out DepartmentDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await departments.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", d => d.Id);
    }

    /// <summary>Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{departmentId:guid}")]
    [RequirePermission(PermissionCatalogue.OrganizationManage)]
    [SensitiveWrite]
    [ProducesResponseType<DepartmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_UpdateDepartment")]
    public async Task<IActionResult> Update(Guid departmentId, DepartmentUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out DepartmentChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await departments.UpdateAsync(CallerId, departmentId, changes!, version!.Value, cancellationToken));
    }

    [HttpPost("{departmentId:guid}/activate")]
    [RequirePermission(PermissionCatalogue.OrganizationManage)]
    [SensitiveWrite]
    [ProducesResponseType<DepartmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ActivateDepartment")]
    public async Task<IActionResult> Activate(Guid departmentId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await departments.ActivateAsync(CallerId, departmentId, version, cancellationToken))
            : problem!;

    [HttpPost("{departmentId:guid}/deactivate")]
    [RequirePermission(PermissionCatalogue.OrganizationManage)]
    [SensitiveWrite]
    [ProducesResponseType<DepartmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_DeactivateDepartment")]
    public async Task<IActionResult> Deactivate(Guid departmentId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await departments.DeactivateAsync(CallerId, departmentId, version, cancellationToken))
            : problem!;
}
