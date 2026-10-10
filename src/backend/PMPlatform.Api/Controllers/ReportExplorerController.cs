using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Correlation;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Reports;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// SCR-138, the controlled report explorer (ADR-019; TASK-071): compositions of the fields the REPORT_RULES allowlist in force names, and nothing else
/// — no SQL, script, join or formula. A field or a projection the allowlist does not name is 422 <c>REPORT_COLUMN_NOT_SUPPORTED</c> before anything is
/// read; with no REPORT_RULES version in force the explorer is 422 <c>CONFIGURATION_MISSING</c>. Under <c>REPORT_COMPOSE</c> (R02, R03, R07).
/// </summary>
[Route(Collection)]
[Tags("Reports")]
public sealed class ReportExplorerController(IReportExplorerService explorer) : ReportExportControllerBase
{
    private const string Collection = "api/v1/report-explorer";

    /// <summary>A composition run now: one page of the caller's authorised rows. A read that stores nothing.</summary>
    [HttpPost("run")]
    [RequirePermission(PermissionCatalogue.ReportCompose)]
    [NonSensitiveWrite("R-35: runs a report composition; reads the caller's authorised rows and stores nothing")]
    [ProducesResponseType<ReportResultPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_RunExplorer")]
    public async Task<IActionResult> Run(ExplorerRunRequest request, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<FieldError> errors = request.Validate(out ExplorerRunInput input);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await explorer.RunAsync(CallerId, input, paging, cancellationToken));
    }

    /// <summary>A composition as a PDF, XLSX or CSV: 202 with the job's <c>Location</c>.</summary>
    [HttpPost("export")]
    [RequirePermission(PermissionCatalogue.ReportCompose)]
    [RequirePermission(PermissionCatalogue.ReportExport)]
    [SensitiveWrite]
    [ProducesResponseType<ReportJobDetail>(StatusCodes.Status202Accepted, "application/json")]
    [EndpointName("Reports_ExportExplorer")]
    public async Task<IActionResult> Export(ExplorerExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<FieldError> errors = request.Validate(out ExplorerRunInput input, out ReportExportInput output);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Accepted(await explorer.ExportAsync(CallerId, input, output, IdempotencyKey, CorrelationId.Of(HttpContext), cancellationToken));
    }
}

/// <summary>MOD-061: the allowlist in force, as the explorer offers it — each field's type, its operators when filterable, its values, its source's semantic state.</summary>
[Route(Collection)]
[Tags("Reports")]
public sealed class ReportAllowlistEntriesController(IReportExplorerService explorer) : AdministrationControllerBase
{
    private const string Collection = "api/v1/report-allowlist-entries";

    [HttpGet]
    [RequirePermission(PermissionCatalogue.ReportCompose)]
    [ProducesResponseType<ReportAllowlistEntryPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_ListReportAllowlistEntries")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await explorer.ListFieldsAsync(CallerId, paging, cancellationToken));
    }
}
