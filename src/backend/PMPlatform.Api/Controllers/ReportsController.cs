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
/// FG-02 at runtime (TASK-071): the ten reports of ADR-006, each run over the projects the caller may see, every cell authorised on its own source
/// projection and carrying its freshness and as-of, with each projection's coverage beside the rows. A role selects a report and grants no data; a
/// report the caller may not run is 404 (R-47). An export is a job (R-7): 202 with its <c>Location</c>, generated asynchronously and authorised
/// again when it is downloaded.
/// </summary>
[Route(Collection)]
[Tags("Reports")]
public sealed class ReportsController(IReportService reports) : ReportExportControllerBase
{
    private const string Collection = "api/v1/reports";

    /// <summary>SCR-130's catalogue: the PUBLISHED reports the caller may run.</summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<ReportCataloguePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_ListReports")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await reports.ListAsync(CallerId, paging, cancellationToken));
    }

    /// <summary>The report as the caller may run it: its columns, its parameters with the options the caller may choose, its formats and what the caller may do.</summary>
    [HttpGet("{reportCode}")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<ReportView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_GetReport")]
    public async Task<IActionResult> Get(string reportCode, CancellationToken cancellationToken) =>
        ReportRoute.Parse(reportCode) is { } code ? Respond(await reports.GetAsync(CallerId, code, cancellationToken)) : Failure(AdministrationError.NotFound);

    /// <summary>One page of the report run now (RPT-API-005): a read that stores nothing.</summary>
    [HttpPost("{reportCode}/run")]
    [AllowAnyAuthenticatedUser]
    [NonSensitiveWrite("R-35: runs a report; reads the caller's authorised rows and stores nothing")]
    [ProducesResponseType<ReportResultPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_RunReport")]
    public async Task<IActionResult> Run(string reportCode, ReportRunRequest request, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<FieldError> errors = request.Validate(out ReportRunInput input);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return ReportRoute.Parse(reportCode) is not { } code ? Failure(AdministrationError.NotFound)
            : errors.Count > 0 ? ValidationFailed(errors)
            : Respond(await reports.RunAsync(CallerId, code, input, paging, cancellationToken));
    }

    /// <summary>MOD-060: the report as a PDF, XLSX or CSV (ADR-005). 202 with the job's <c>Location</c>; the same <c>Idempotency-Key</c> answers with the same job.</summary>
    [HttpPost("{reportCode}/export")]
    [RequirePermission(PermissionCatalogue.ReportExport)]
    [SensitiveWrite]
    [ProducesResponseType<ReportJobDetail>(StatusCodes.Status202Accepted, "application/json")]
    [EndpointName("Reports_ExportReport")]
    public async Task<IActionResult> Export(string reportCode, ReportExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<FieldError> errors = request.Validate(out ReportRunInput input, out ReportExportInput output);
        return ReportRoute.Parse(reportCode) is not { } code ? Failure(AdministrationError.NotFound)
            : errors.Count > 0 ? ValidationFailed(errors)
            : Accepted(await reports.ExportAsync(CallerId, code, input, output, IdempotencyKey, CorrelationId.Of(HttpContext), cancellationToken));
    }
}

/// <summary>R-7 for every export: 202 with <c>Location: /api/v1/report-jobs/{id}</c>, and <c>Idempotent-Replayed</c> when the key had already made the job.</summary>
public abstract class ReportExportControllerBase : AdministrationControllerBase
{
    private const string ReplayedHeader = "Idempotent-Replayed";

    /// <summary>[SensitiveWrite] has already refused a request without a uuid key (R-36).</summary>
    private protected Guid IdempotencyKey => Guid.Parse(Request.Headers[SensitiveWriteAttribute.HeaderName].ToString());

    private protected IActionResult Accepted(AdministrationResult<ReportExportOutcome> outcome)
    {
        if (!outcome.Succeeded)
        {
            return Failure(outcome.Error);
        }

        if (outcome.Value.Replayed)
        {
            Response.Headers[ReplayedHeader] = "true";
        }

        return Accepted($"/{ReportJobsController.Collection}/{outcome.Value.Job.Id}", outcome.Value.Job);
    }
}
