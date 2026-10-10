using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>
/// SCR-138, the controlled report explorer (ADR-019; FG-02 §11.3): compositions of the fields the REPORT_RULES allowlist in force names, over
/// the projects the caller may see — no SQL, no script, no join or formula of its own. A field or a projection the allowlist does not name is
/// refused (422 <c>REPORT_COLUMN_NOT_SUPPORTED</c>) before anything is read. Under <c>REPORT_COMPOSE</c>, which ADR-019 grants R02, R03 and R07.
/// </summary>
public interface IReportExplorerService
{
    /// <summary>The allowlist in force, as the explorer offers it (MOD-061).</summary>
    public Task<ReportAllowlistEntryPage> ListFieldsAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<ReportResultPage>> RunAsync(Guid callerId, ExplorerRunInput input, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<ReportExportOutcome>> ExportAsync(
        Guid callerId, ExplorerRunInput input, ReportExportInput output, Guid idempotencyKey, Guid correlationId, CancellationToken cancellationToken);
}
