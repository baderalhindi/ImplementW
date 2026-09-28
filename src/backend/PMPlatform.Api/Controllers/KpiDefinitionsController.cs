using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>The KPI catalogue (TASK-034): stable KPI identities under the governed lifecycle; never deleted (RETAIN).</summary>
[Route(Collection)]
[Tags("MasterDataConfig")]
public sealed class KpiDefinitionsController(IKpiDefinitionAdministrationService kpiDefinitions) : AdministrationControllerBase
{
    private const string Collection = "api/v1/kpi-definitions";

    /// <summary>Filters: <c>lifecycleState</c> (a set), <c>q</c>. By code.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.MasterDataView)]
    [ProducesResponseType<KpiDefinitionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_ListKpiDefinitions")]
    public async Task<IActionResult> List(
        [FromQuery] string? lifecycleState, [FromQuery] string? q, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        KpiDefinitionQuery query = new(
            QueryParameters.EnumSet<GovernedLifecycleState>(lifecycleState, "lifecycleState", errors), q, QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await kpiDefinitions.ListAsync(query, cancellationToken));
    }

    [HttpGet("{kpiDefinitionId:guid}")]
    [RequirePermission(PermissionCatalogue.MasterDataView)]
    [ProducesResponseType<KpiDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_GetKpiDefinition")]
    public async Task<IActionResult> Get(Guid kpiDefinitionId, CancellationToken cancellationToken) =>
        Respond(await kpiDefinitions.GetAsync(kpiDefinitionId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiDefinitionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("MasterDataConfig_CreateKpiDefinition")]
    public async Task<IActionResult> Create(KpiDefinitionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out KpiDefinitionDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await kpiDefinitions.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", d => d.Id);
    }

    /// <summary>Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{kpiDefinitionId:guid}")]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_UpdateKpiDefinition")]
    public async Task<IActionResult> Update(Guid kpiDefinitionId, KpiDefinitionUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out KpiDefinitionChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await kpiDefinitions.UpdateAsync(CallerId, kpiDefinitionId, changes!, version!.Value, cancellationToken));
    }

    [HttpPost("{kpiDefinitionId:guid}/validate")]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_ValidateKpiDefinition")]
    public async Task<IActionResult> Validate(Guid kpiDefinitionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await kpiDefinitions.ValidateAsync(CallerId, kpiDefinitionId, version, cancellationToken))
            : problem!;

    [HttpPost("{kpiDefinitionId:guid}/publish")]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_PublishKpiDefinition")]
    public async Task<IActionResult> Publish(Guid kpiDefinitionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await kpiDefinitions.PublishAsync(CallerId, kpiDefinitionId, version, cancellationToken))
            : problem!;

    [HttpPost("{kpiDefinitionId:guid}/retire")]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_RetireKpiDefinition")]
    public async Task<IActionResult> Retire(Guid kpiDefinitionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await kpiDefinitions.RetireAsync(CallerId, kpiDefinitionId, version, cancellationToken))
            : problem!;
}
