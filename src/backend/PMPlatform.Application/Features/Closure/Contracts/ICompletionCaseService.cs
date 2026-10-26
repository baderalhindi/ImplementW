using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Closure.Contracts;

/// <summary>
/// WF-10's completion stage (TASK-063): a readiness-gated case that takes an ACTIVE project to COMPLETED, raised by its people — an entity
/// Project Manager included (ADR-013) — reviewed by AHDA and decided through WF-11. No progress figure, task or date completes a project:
/// only this case, once approved and then effected (BR-CLO-009, CLO-CC-16). A collection is of one project and is empty for one the caller
/// may not see; a case the caller may not see is 404 (R-47). Each command is one edge (R-4) and answers with the case.
/// </summary>
public interface ICompletionCaseService
{
    public Task<CompletionCasePage> ListAsync(Guid callerId, CloseoutCaseQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> GetAsync(Guid callerId, Guid completionCaseId, CancellationToken cancellationToken);

    /// <summary>Raises a DRAFT for an ACTIVE project with no completion case open. CLOSEOUT_RAISE.</summary>
    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> CreateAsync(Guid callerId, CompletionCaseDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces the fields of a DRAFT or RETURNED case. Requires the caller's version (R-21). CLOSEOUT_RAISE.</summary>
    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> UpdateAsync(
        Guid callerId, Guid completionCaseId, CompletionCaseChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>Deletes a DRAFT never submitted (HARD_DRAFT). Null when deleted, or when there is nothing the caller may see to delete.</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Evaluates the readiness criteria of a DRAFT or RETURNED case against the source modules and records the result. CLOSEOUT_RAISE.</summary>
    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> EvaluateReadinessAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Accepts a failed, waivable criterion of a DRAFT or RETURNED case as an exception, with why. CLOSEOUT_WAIVE, internal users.</summary>
    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> WaiveCheckAsync(
        Guid callerId, Guid completionCaseId, ReadinessWaiver waiver, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// DRAFT or RETURNED → SUBMITTED: the readiness evaluated afresh — the revision's frozen snapshot — and refused when NOT_READY; the
    /// actual completion date and narrative required. A RETURNED case comes back as the next revision. CLOSEOUT_RAISE.
    /// </summary>
    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> SubmitAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED or RETURNED → WITHDRAWN. CLOSEOUT_RAISE.</summary>
    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> WithdrawAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED → UNDER_REVIEW, starting the revision's WF-11 run (COMPLETION route) in the same transaction. CLOSEOUT_REVIEW, internal users.</summary>
    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> StartReviewAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// APPROVED → EFFECTED: the readiness revalidated, then in one transaction the project moves ACTIVE → COMPLETED. A refusal leaves the
    /// case APPROVED and the project as it was; the command is safe to retry. CLOSEOUT_ACTIVATE, internal users.
    /// </summary>
    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> ActivateAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken);
}
