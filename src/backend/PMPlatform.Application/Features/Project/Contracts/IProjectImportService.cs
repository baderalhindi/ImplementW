using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// Project import from RPMO's workbook (the 2 Nov 2026 review, R3-4): download the template, validate a workbook and map its names, submit it as
/// one batch with a note, and have a second person approve it — which applies it — or return it. An applied batch is rolled back whole while
/// none of its projects has been touched. PROJECT_IMPORT prepares and submits, within the importer's scope; PROJECT_IMPORT_APPROVE decides,
/// never on one's own batch. Imported projects enter in their mapped state as in-flight intake (ADR-014): no registration approval runs again.
/// </summary>
public interface IProjectImportService
{
    /// <summary>The .xlsx template, its lists drawn from live master data, with a version stamp.</summary>
    public Task<AdministrationResult<byte[]>> TemplateAsync(Guid callerId, CancellationToken cancellationToken);

    public Task<AdministrationResult<ProjectImportPreview>> ValidateAsync(
        Guid callerId, ProjectImportUpload upload, ProjectImportPlan plan, CancellationToken cancellationToken);

    /// <summary>Validates again and, when the workbook is submittable, saves it as a SUBMITTED batch and remembers its mappings.</summary>
    public Task<AdministrationResult<ProjectImportBatchDetail>> SubmitAsync(
        Guid callerId, ProjectImportUpload upload, ProjectImportPlan plan, NarrativeText? note, CancellationToken cancellationToken);

    public Task<ProjectImportBatchPage> ListAsync(Guid callerId, ProjectImportBatchQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectImportBatchDetail>>> GetAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken);

    /// <summary>The batch's plan as it would apply now, for the approver to review.</summary>
    public Task<AdministrationResult<ProjectImportPreview>> PreviewAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken);

    public Task<AdministrationResult<ProjectImportFile>> FileAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken);

    /// <summary>SUBMITTED → APPROVED: applies the plan in one transaction, or refuses it whole when a row no longer validates.</summary>
    public Task<AdministrationResult<Versioned<ProjectImportBatchDetail>>> ApproveAsync(Guid callerId, Guid batchId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED → RETURNED, with the reason the importer reads.</summary>
    public Task<AdministrationResult<Versioned<ProjectImportBatchDetail>>> ReturnAsync(
        Guid callerId, Guid batchId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>APPROVED → ROLLED_BACK: removes the projects the batch created, while none has changed since and the batch updated none.</summary>
    public Task<AdministrationResult<Versioned<ProjectImportBatchDetail>>> RollBackAsync(Guid callerId, Guid batchId, uint? expectedVersion, CancellationToken cancellationToken);
}
