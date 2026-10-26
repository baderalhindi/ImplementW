using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Closure.Contracts;

/// <summary>
/// WF-10's post-project obligations (TASK-063): responsibilities that survive completion, with their owner and due date, open while the
/// project is COMPLETED and settled before it closes (WF-10 §7.2). Recorded and kept by the project's people (CLOSEOUT_RAISE); waived only
/// by AHDA (CLOSEOUT_WAIVE, internal users). Each command is one edge and answers with the obligation.
/// </summary>
public interface IPostProjectObligationService
{
    public Task<PostProjectObligationPage> ListAsync(Guid callerId, PostProjectObligationQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> GetAsync(Guid callerId, Guid obligationId, CancellationToken cancellationToken);

    /// <summary>Records an OPEN obligation against an open or effected completion case, or an open terminal closure case.</summary>
    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> CreateAsync(Guid callerId, PostProjectObligationDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces an open obligation's fields. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> UpdateAsync(
        Guid callerId, Guid obligationId, PostProjectObligationChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>OPEN → IN_PROGRESS.</summary>
    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> StartAsync(Guid callerId, Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>OPEN or IN_PROGRESS → SATISFIED.</summary>
    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> SatisfyAsync(Guid callerId, Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>OPEN or IN_PROGRESS → CANCELLED: recorded in error, or no longer owed.</summary>
    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> CancelAsync(Guid callerId, Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>OPEN or IN_PROGRESS → WAIVED: AHDA releases it. CLOSEOUT_WAIVE, internal users.</summary>
    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> WaiveAsync(Guid callerId, Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken);
}
