using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// The Approved Budget's versions (TASK-052, ADR-008). A version is opened DRAFT, given its referenced document through
/// DocumentManagement (edge 38), and submitted to WF-11 (edge 26); WF-11's approval makes it ACTIVE — the budget of record — and
/// supersedes the previous ACTIVE version, which is kept as it was. SAR only, project total only.
/// </summary>
public interface IFinancialCommitmentService
{
    /// <summary>The project's versions, newest first.</summary>
    public Task<FinancialCommitmentPage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<FinancialCommitmentDetail>>> GetAsync(Guid callerId, Guid commitmentId, CancellationToken cancellationToken);

    /// <summary>Opens the next Approved Budget version as a DRAFT. One version is on its way at a time.</summary>
    public Task<AdministrationResult<Versioned<FinancialCommitmentDetail>>> CreateAsync(Guid callerId, FinancialCommitmentDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces a DRAFT or RETURNED version's figures. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<FinancialCommitmentDetail>>> UpdateAsync(
        Guid callerId, Guid commitmentId, FinancialCommitmentChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>HARD_DRAFT: deletes a DRAFT and ends its document links. One that is not there is not an error (R-40).</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid commitmentId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// DRAFT or RETURNED → SUBMITTED to WF-11, once a referenced document is held (ADR-008 gate) and, for a change to an ACTIVE Approved
    /// Budget, an applicable WF-08 change authorisation is named (TASK-060); WF-11's approval applies it as the version activates.
    /// </summary>
    public Task<AdministrationResult<Versioned<FinancialCommitmentDetail>>> SubmitAsync(
        Guid callerId, Guid commitmentId, CommitmentSubmission submission, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<CommitmentDocumentDetail>> GetDocumentsAsync(Guid callerId, Guid commitmentId, CancellationToken cancellationToken);

    /// <summary>Attaches a document to a DRAFT or RETURNED version and pins one of its CLEAN versions as the reference.</summary>
    public Task<AdministrationResult<EvidenceReferenceDetail>> AttachDocumentAsync(
        Guid callerId, Guid commitmentId, CommitmentDocumentAttachment attachment, CancellationToken cancellationToken);

    public Task<AdministrationResult<EvidenceReferenceDetail>> WithdrawDocumentAsync(Guid callerId, Guid commitmentId, Guid evidenceReferenceId, CancellationToken cancellationToken);
}
