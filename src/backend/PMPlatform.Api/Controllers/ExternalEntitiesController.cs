using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Controllers;

/// <summary>ADM-013 Entities (TASK-031, ADR-013). Never deleted (RETAIN): ACTIVE ↔ SUSPENDED, then RETIRED, which is terminal.</summary>
[Route(Collection)]
[Tags("IdentityAccess")]
public sealed class ExternalEntitiesController(IExternalEntityAdministrationService entities) : AdministrationControllerBase
{
    private const string Collection = "api/v1/external-entities";

    /// <summary>Filters: <c>status</c> (set), <c>entityTypeItemId</c>, <c>q</c>. By code.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.OrganizationView)]
    [ProducesResponseType<ExternalEntityPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListExternalEntities")]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] Guid? entityTypeItemId, [FromQuery] string? q, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        ExternalEntityQuery query = new(
            QueryParameters.EnumSet<ExternalEntityStatus>(status, "status", errors), entityTypeItemId, q, QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await entities.ListAsync(CallerId, query, cancellationToken));
    }

    [HttpGet("{externalEntityId:guid}")]
    [RequirePermission(PermissionCatalogue.OrganizationView)]
    [ProducesResponseType<ExternalEntityDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetExternalEntity")]
    public async Task<IActionResult> Get(Guid externalEntityId, CancellationToken cancellationToken) =>
        Respond(await entities.GetAsync(CallerId, externalEntityId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCatalogue.OrganizationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalEntityDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("IdentityAccess_CreateExternalEntity")]
    public async Task<IActionResult> Create(ExternalEntityCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ExternalEntityDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await entities.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", e => e.Id);
    }

    /// <summary>Requires <c>If-Match</c> (R-21). A RETIRED entity takes no write.</summary>
    [HttpPut("{externalEntityId:guid}")]
    [RequirePermission(PermissionCatalogue.OrganizationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalEntityDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_UpdateExternalEntity")]
    public async Task<IActionResult> Update(Guid externalEntityId, ExternalEntityUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ExternalEntityChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await entities.UpdateAsync(CallerId, externalEntityId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>ACTIVE → SUSPENDED: the entity's people can no longer sign in or act.</summary>
    [HttpPost("{externalEntityId:guid}/suspend")]
    [RequirePermission(PermissionCatalogue.OrganizationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalEntityDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_SuspendExternalEntity")]
    public async Task<IActionResult> Suspend(Guid externalEntityId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await entities.SuspendAsync(CallerId, externalEntityId, version, cancellationToken))
            : problem!;

    /// <summary>SUSPENDED → ACTIVE.</summary>
    [HttpPost("{externalEntityId:guid}/activate")]
    [RequirePermission(PermissionCatalogue.OrganizationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalEntityDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ActivateExternalEntity")]
    public async Task<IActionResult> Activate(Guid externalEntityId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await entities.ActivateAsync(CallerId, externalEntityId, version, cancellationToken))
            : problem!;

    /// <summary>ACTIVE or SUSPENDED → RETIRED, terminal.</summary>
    [HttpPost("{externalEntityId:guid}/retire")]
    [RequirePermission(PermissionCatalogue.OrganizationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalEntityDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_RetireExternalEntity")]
    public async Task<IActionResult> Retire(Guid externalEntityId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await entities.RetireAsync(CallerId, externalEntityId, version, cancellationToken))
            : problem!;
}
