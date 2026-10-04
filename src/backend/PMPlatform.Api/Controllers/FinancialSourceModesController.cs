using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.FinancialKpi;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-14 (TASK-052, ADR-008): the source mode of each financial field of a project — MANUAL at launch, INTEGRATED and HYBRID
/// built and unconnected. An INTEGRATED field takes no manual figure and never returns to another mode.
/// </summary>
[Route(Collection)]
[Tags("FinancialKpi")]
public sealed class FinancialSourceModesController(IFinancialSourceModeService modes) : AdministrationControllerBase
{
    private const string Collection = "api/v1/financial-source-modes";

    /// <summary>Every financial field of the project with its mode; a field never configured is MANUAL, with no <c>id</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<FinancialSourceModePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ListFinancialSourceModes")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await modes.ListAsync(CallerId, projectId!.Value, paging, cancellationToken));

    [HttpGet("{sourceModeId:guid}")]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<FinancialSourceModeDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_GetFinancialSourceMode")]
    public async Task<IActionResult> Get(Guid sourceModeId, CancellationToken cancellationToken) =>
        Respond(await modes.GetAsync(CallerId, sourceModeId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCatalogue.FinancialSourceManage)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialSourceModeDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("FinancialKpi_CreateFinancialSourceMode")]
    public async Task<IActionResult> Create(FinancialSourceModeCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out FinancialSourceModeDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await modes.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", m => m.Id!.Value);
    }

    /// <summary>The field's mode. INTEGRATED is locked (409 <c>FINANCIAL_SOURCE_MODE_LOCKED</c>). Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{sourceModeId:guid}")]
    [RequirePermission(PermissionCatalogue.FinancialSourceManage)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialSourceModeDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_UpdateFinancialSourceMode")]
    public async Task<IActionResult> Update(Guid sourceModeId, FinancialSourceModeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate() is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await modes.UpdateAsync(CallerId, sourceModeId, request.SourceMode!.Value, version!.Value, cancellationToken));
    }
}
