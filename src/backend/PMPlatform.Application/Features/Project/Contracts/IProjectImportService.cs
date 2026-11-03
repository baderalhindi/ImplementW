using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

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

    /// <summary>Validates a workbook of up to 200 rows at once; queues a larger one for the background.</summary>
    public Task<AdministrationResult<ProjectImportValidationOutcome>> ValidateAsync(
        Guid callerId, ProjectImportUpload upload, ProjectImportPlan plan, CancellationToken cancellationToken);

    /// <summary>A background validation, to the person who asked for it only.</summary>
    public Task<AdministrationResult<ProjectImportValidationDetail>> GetValidationAsync(Guid callerId, Guid validationId, CancellationToken cancellationToken);

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

/// <summary>Runs the queued background validations (review §4, step 3), one pass at a time.</summary>
public interface IProjectImportValidationRunner
{
    /// <summary>Validates up to <paramref name="batchSize"/> queued workbooks; returns how many it finished.</summary>
    public Task<int> RunAsync(int batchSize, CancellationToken cancellationToken);
}

/// <summary>
/// Requests to add a value the platform's lists lack (review §3): an importer (PROJECT_IMPORT) asks, administrators (MASTER_DATA_MANAGE or
/// ORGANIZATION_MANAGE) are notified and mark each added or declined, and the importer is notified in turn.
/// </summary>
public interface IProjectImportValueRequestService
{
    /// <summary>Asks for the value; an OPEN request for the same name is answered instead of a second one.</summary>
    public Task<AdministrationResult<ProjectImportValueRequestDetail>> RequestAsync(
        Guid callerId, ProjectImportDimension dimension, string sourceText, CancellationToken cancellationToken);

    public Task<AdministrationResult<ProjectImportValueRequestPage>> ListAsync(
        Guid callerId, ProjectImportValueRequestStatus? status, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectImportValueRequestDetail>>> ResolveAsync(
        Guid callerId, Guid requestId, ProjectImportValueRequestStatus status, NarrativeText? note, uint? expectedVersion, CancellationToken cancellationToken);
}
