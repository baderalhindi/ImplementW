using PMPlatform.Application.Features.Approval.Contracts.Events;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// The source-callback contract (event-conventions §8 row 5, EV-11): one implementation per source module, in that
/// module's <c>EventHandlers</c> folder, applying <see cref="ApprovalOutcomeRecorded"/> to the subject it names. It is
/// the same contract for every source; Approval knows a source only by its <see cref="SubjectModule"/> name.
/// </summary>
/// <remarks>
/// It runs inside the dispatch transaction that marks the outcome delivered, so a change saved through the request's
/// context commits exactly when delivery does: the outcome is applied once, however often dispatch is retried. The
/// handler must still be idempotent on its own (EV-4): record <see cref="ApprovalOutcomeRecorded"/>'s
/// <c>IdempotencyKey</c> on the subject under a unique constraint, and refuse an outcome for a revision that is no longer
/// current as stale (EV-5). A handler that throws leaves nothing applied, and the outcome is delivered again later.
/// </remarks>
public interface IApprovalOutcomeHandler
{
    /// <summary>The ADR-003 module name that <see cref="ApprovalSubject.Module"/> carries for this source.</summary>
    public string SubjectModule { get; }

    public Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken);
}
