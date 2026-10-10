using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>
/// SCR-140 Export History and the secure download (FG-02 §9.3, §13.5). A job and its output are their requester's: another person's job does
/// not exist for them (R-47). A download is authorised again at download time on the same rules as the data it holds — the requester must still
/// be of the report's audience, still hold the export permission over every project in it, and still be shown every value it revealed — never
/// on the opaque key alone (BR-RPT-042; TASK-071 acceptance criterion 2).
/// </summary>
public interface IReportJobService
{
    public Task<ReportJobPage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ReportJobDetail>>> GetAsync(Guid callerId, Guid jobId, CancellationToken cancellationToken);

    /// <summary>A job not yet finished → CANCELLED; nothing it may have rendered is kept (US-RPT-SYS-036).</summary>
    public Task<AdministrationResult<Versioned<ReportJobDetail>>> CancelAsync(Guid callerId, Guid jobId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The output's bytes, when its requester may still see everything it holds (403 otherwise, audited).</summary>
    public Task<AdministrationResult<ReportOutputDownload>> OpenContentAsync(Guid callerId, Guid jobId, CancellationToken cancellationToken);
}
