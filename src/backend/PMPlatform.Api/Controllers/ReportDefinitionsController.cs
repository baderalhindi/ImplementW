using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Reports;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-037 Report Configuration (TASK-071; FG-02 §11) on the governed lifecycle — DRAFT, VALIDATED, PUBLISHED, RETIRED — with author, reviewer and
/// publisher three different people, administered under <c>CONFIGURATION_VIEW</c> and <c>CONFIGURATION_MANAGE</c> as ADM-036 is. A version is of one
/// of the ten reports ADR-006 delivers; a PUBLISHED one is immutable and is replaced by publishing its successor, never retired alone. Configuring a
/// report grants no business data (BR-RPT-048). The register of projections and fields a version may name is <c>GET /dashboard-projections</c>.
/// </summary>
[Route(Collection)]
[Tags("Reports")]
public sealed class ReportDefinitionsController(IReportDefinitionService definitions) : AdministrationControllerBase
{
    private const string Collection = "api/v1/report-definitions";

    [HttpGet]
    [RequirePermission(PermissionCatalogue.ConfigurationView)]
    [ProducesResponseType<ReportDefinitionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_ListReportDefinitions")]
    public async Task<IActionResult> List(
        [FromQuery] ReportCode? code, [FromQuery] GovernedLifecycleState? lifecycleState, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await definitions.ListAsync(new ReportDefinitionQuery(code, lifecycleState), paging, cancellationToken));
    }

    [HttpGet("{definitionId:guid}")]
    [RequirePermission(PermissionCatalogue.ConfigurationView)]
    [ProducesResponseType<ReportDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_GetReportDefinition")]
    public async Task<IActionResult> Get(Guid definitionId, CancellationToken cancellationToken) =>
        Respond(await definitions.GetAsync(definitionId, cancellationToken));

    /// <summary>Opens the report's next version as a DRAFT copy of its PUBLISHED version; one version is on its way at a time (409 <c>REPORT_VERSION_OPEN</c>).</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ReportDefinitionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Reports_CreateReportDefinition")]
    public async Task<IActionResult> Create(ReportDefinitionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ReportCode code) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await definitions.CreateAsync(CallerId, code, cancellationToken), $"/{Collection}", d => d.Id);
    }

    /// <summary>A DRAFT's content, as a whole, by its author. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{definitionId:guid}")]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ReportDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_UpdateReportDefinition")]
    public async Task<IActionResult> Update(Guid definitionId, ReportDefinitionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ReportDefinitionContent? content) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await definitions.UpdateAsync(CallerId, definitionId, content!, version!.Value, cancellationToken));
    }

    /// <summary>DRAFT → VALIDATED by a reviewer who is not the author, once FG-02 §11.2's validation passes (422 <c>REPORT_DEFINITION_INVALID</c> names each refusal).</summary>
    [HttpPost("{definitionId:guid}/validate")]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ReportDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_ValidateReportDefinition")]
    public async Task<IActionResult> Validate(Guid definitionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await definitions.ValidateAsync(CallerId, definitionId, version, cancellationToken))
            : problem!;

    /// <summary>VALIDATED → PUBLISHED by a third person, validated again; the version it replaces is retired in the same save.</summary>
    [HttpPost("{definitionId:guid}/publish")]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ReportDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_PublishReportDefinition")]
    public async Task<IActionResult> Publish(Guid definitionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await definitions.PublishAsync(CallerId, definitionId, version, cancellationToken))
            : problem!;

    /// <summary>An abandoned DRAFT or VALIDATED version → RETIRED. A PUBLISHED one is 409 <c>REPORT_RETIREMENT_NOT_PERMITTED</c>: it is replaced, not removed.</summary>
    [HttpPost("{definitionId:guid}/retire")]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ReportDefinitionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_RetireReportDefinition")]
    public async Task<IActionResult> Retire(Guid definitionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await definitions.RetireAsync(CallerId, definitionId, version, cancellationToken))
            : problem!;
}
