using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// SCR-140 Export History (TASK-071): the caller's report jobs and their outputs (R-7). A job is its requester's alone; another person's is 404 (R-47).
/// The output is downloaded at <c>…/content</c>, authorised then on the same rules as the data it holds — the requester must still see every project
/// and every value in it — and refused 403 otherwise (TASK-071 acceptance criterion 2); an expired output is 409 <c>REPORT_OUTPUT_EXPIRED</c>.
/// </summary>
[Route(Collection)]
[Tags("Reports")]
public sealed class ReportJobsController(IReportJobService jobs) : AdministrationControllerBase
{
    internal const string Collection = "api/v1/report-jobs";

    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<ReportJobPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_ListReportJobs")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await jobs.ListAsync(CallerId, paging, cancellationToken));
    }

    [HttpGet("{jobId:guid}")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<ReportJobDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_GetReportJob")]
    public async Task<IActionResult> Get(Guid jobId, CancellationToken cancellationToken) =>
        Respond(await jobs.GetAsync(CallerId, jobId, cancellationToken));

    /// <summary>A job not yet finished → CANCELLED; nothing it may have rendered is kept. 409 <c>REPORT_JOB_NOT_CANCELLABLE</c> once it has finished.</summary>
    [HttpPost("{jobId:guid}/cancel")]
    [AllowAnyAuthenticatedUser]
    [SensitiveWrite]
    [ProducesResponseType<ReportJobDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_CancelReportJob")]
    public async Task<IActionResult> Cancel(Guid jobId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await jobs.CancelAsync(CallerId, jobId, version, cancellationToken))
            : problem!;

    /// <summary>The output's bytes (R-8), authorised now; never cached.</summary>
    [HttpGet("{jobId:guid}/content")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream")]
    [EndpointName("Reports_DownloadReportOutput")]
    public async Task<IActionResult> Download(Guid jobId, CancellationToken cancellationToken) =>
        Attachment(await jobs.OpenContentAsync(CallerId, jobId, cancellationToken));
}
