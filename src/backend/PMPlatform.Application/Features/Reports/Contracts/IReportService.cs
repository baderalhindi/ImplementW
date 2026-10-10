using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>
/// FG-02's published reports at runtime (TASK-071; FG-02 §21 Report Catalogue, Query and Execution Services). A person's roles select the
/// reports they may run — an external entity's person, only the entity report set (ADR-013) — and select nothing else: every row and cell is
/// authorised on its projection's own permission, before any source is read. A report the caller may not run does not exist for them (R-47).
/// </summary>
public interface IReportService
{
    /// <summary>The PUBLISHED reports the caller may run (RPT-API-001).</summary>
    public Task<ReportCataloguePage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The report as the caller may run it: columns, parameters with their authorised options, and permitted actions.</summary>
    public Task<AdministrationResult<ReportView>> GetAsync(Guid callerId, ReportCode code, CancellationToken cancellationToken);

    /// <summary>Runs the report on screen (RPT-API-005): one page of its authorised rows, with each projection's freshness and coverage.</summary>
    public Task<AdministrationResult<ReportResultPage>> RunAsync(Guid callerId, ReportCode code, ReportRunInput input, PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// Requests a PDF, XLSX or CSV of the report (RPT-API-009): a REQUESTED job, generated asynchronously (DC-RPT-01). The caller's key makes a
    /// repeated request answer with the same job.
    /// </summary>
    public Task<AdministrationResult<ReportExportOutcome>> ExportAsync(
        Guid callerId, ReportCode code, ReportRunInput input, ReportExportInput output, Guid idempotencyKey, Guid correlationId, CancellationToken cancellationToken);
}
