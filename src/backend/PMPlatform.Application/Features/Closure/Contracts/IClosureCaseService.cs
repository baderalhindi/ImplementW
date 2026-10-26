using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Closure.Contracts;

/// <summary>
/// WF-10's closure stage (TASK-063): a readiness-gated case, distinct from and after completion, that takes a COMPLETED project to CLOSED —
/// or, on the terminal path, a SUSPENDED project that will not resume, without completing it (WF-10 §9). Reviewed by AHDA and decided
/// through WF-11; CLOSED is terminal and read-only (BR-CLO-020). The commands mirror <see cref="ICompletionCaseService"/>'s.
/// </summary>
public interface IClosureCaseService
{
    public Task<ClosureCasePage> ListAsync(Guid callerId, CloseoutCaseQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> GetAsync(Guid callerId, Guid closureCaseId, CancellationToken cancellationToken);

    /// <summary>
    /// Raises a DRAFT for a COMPLETED project, following its effected completion case, or for a SUSPENDED one on the terminal path, with no
    /// closure case open. CLOSEOUT_RAISE.
    /// </summary>
    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> CreateAsync(Guid callerId, ClosureCaseDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> UpdateAsync(
        Guid callerId, Guid closureCaseId, ClosureCaseChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> EvaluateReadinessAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> WaiveCheckAsync(
        Guid callerId, Guid closureCaseId, ReadinessWaiver waiver, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>DRAFT or RETURNED → SUBMITTED, with the closure narrative and readiness not NOT_READY. CLOSEOUT_RAISE.</summary>
    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> SubmitAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> WithdrawAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED → UNDER_REVIEW, starting the revision's WF-11 run (CLOSURE route). CLOSEOUT_REVIEW, internal users.</summary>
    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> StartReviewAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// APPROVED → EFFECTED: the readiness revalidated, then in one transaction the project moves to CLOSED, its open suspension ends on the
    /// terminal path, and its per-project access ends (ADR-013). CLOSEOUT_ACTIVATE, internal users.
    /// </summary>
    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> ActivateAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken);
}
