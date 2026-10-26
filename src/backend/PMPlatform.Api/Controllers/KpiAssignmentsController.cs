using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.FinancialKpi;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-14 KPI Performance (TASK-052): catalogue KPIs assigned to a project, each once; and the portfolio aggregate of their
/// latest published measurements, combined only within one unit.
/// </summary>
[Tags("FinancialKpi")]
[OmitMaskedFields]
public sealed class KpiAssignmentsController(IKpiAssignmentService assignments) : AdministrationControllerBase
{
    private const string Collection = "api/v1/kpi-assignments";

    /// <summary>The project's assignments, newest first, each with its KPI's unit.</summary>
    [HttpGet(Collection)]
    [RequirePermission(PermissionCatalogue.KpiView)]
    [ProducesResponseType<KpiAssignmentPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ListKpiAssignments")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await assignments.ListAsync(CallerId, projectId!.Value, paging, cancellationToken));

    [HttpGet(Collection + "/{assignmentId:guid}")]
    [RequirePermission(PermissionCatalogue.KpiView)]
    [ProducesResponseType<KpiAssignmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_GetKpiAssignment")]
    public async Task<IActionResult> Get(Guid assignmentId, CancellationToken cancellationToken) =>
        Respond(await assignments.GetAsync(CallerId, assignmentId, cancellationToken));

    /// <summary>Assigns a PUBLISHED KPI to the project, ACTIVE. A KPI is assigned to a project once (409 <c>KPI_ALREADY_ASSIGNED</c>).</summary>
    [HttpPost(Collection)]
    [RequirePermission(PermissionCatalogue.KpiManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiAssignmentDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("FinancialKpi_CreateKpiAssignment")]
    public async Task<IActionResult> Create(KpiAssignmentCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out KpiAssignmentDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await assignments.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", a => a.Id);
    }

    /// <summary>The assignment's owner and frequency, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut(Collection + "/{assignmentId:guid}")]
    [RequirePermission(PermissionCatalogue.KpiManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiAssignmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_UpdateKpiAssignment")]
    public async Task<IActionResult> Update(Guid assignmentId, KpiAssignmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out KpiAssignmentChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await assignments.UpdateAsync(CallerId, assignmentId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>ACTIVE → SUSPENDED: no target version or measurement is recorded while suspended.</summary>
    [HttpPost(Collection + "/{assignmentId:guid}/suspend")]
    [RequirePermission(PermissionCatalogue.KpiManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiAssignmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_SuspendKpiAssignment")]
    public Task<IActionResult> Suspend(Guid assignmentId, CancellationToken cancellationToken) => TransitionAsync(assignmentId, KpiAssignmentStatus.Suspended, cancellationToken);

    /// <summary>SUSPENDED → ACTIVE.</summary>
    [HttpPost(Collection + "/{assignmentId:guid}/reactivate")]
    [RequirePermission(PermissionCatalogue.KpiManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiAssignmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ReactivateKpiAssignment")]
    public Task<IActionResult> Reactivate(Guid assignmentId, CancellationToken cancellationToken) => TransitionAsync(assignmentId, KpiAssignmentStatus.Active, cancellationToken);

    /// <summary>ACTIVE or SUSPENDED → RETIRED, final (RETAIN: an assignment is never deleted).</summary>
    [HttpPost(Collection + "/{assignmentId:guid}/retire")]
    [RequirePermission(PermissionCatalogue.KpiManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiAssignmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_RetireKpiAssignment")]
    public Task<IActionResult> Retire(Guid assignmentId, CancellationToken cancellationToken) => TransitionAsync(assignmentId, KpiAssignmentStatus.Retired, cancellationToken);

    /// <summary>
    /// The latest published measurement of each KPI named (<c>kpiDefinitionId</c>, repeated) on each project named
    /// (<c>projectId</c>, repeated, 1 to 200). Values are combined only when every KPI measures in one unit; otherwise
    /// <c>meanValue</c> is null and the aggregate is partial. RAG ratings are always counted.
    /// </summary>
    [HttpGet("api/v1/kpi-portfolio-aggregates")]
    [RequirePermission(PermissionCatalogue.KpiView)]
    [ProducesResponseType<KpiPortfolioAggregate>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_GetKpiPortfolioAggregate")]
    public async Task<IActionResult> Aggregate([FromQuery] Guid[]? kpiDefinitionId, [FromQuery] Guid[]? projectId, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PortfolioQuery.RequireIds(kpiDefinitionId, "kpiDefinitionId", errors);
        PortfolioQuery.RequireIds(projectId, "projectId", errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await assignments.AggregateAsync(CallerId, kpiDefinitionId!, projectId!, cancellationToken));
    }

    private async Task<IActionResult> TransitionAsync(Guid assignmentId, KpiAssignmentStatus status, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await assignments.TransitionAsync(CallerId, assignmentId, status, version, cancellationToken))
            : problem!;
}
