using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.ManagementConcern.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ManagementConcern;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern.EventHandlers;

/// <summary>
/// Applies the outcome of a resolution's WF-11 validation run (ADR-003 §8.2 edges 24, 28): an approval makes the concern RESOLVED; a
/// return, rejection or withdrawal sends it back to IN_PROGRESS as its next revision, its resolution kept for correction — there is
/// no terminal rejection of a resolution (WF-07 §5.6). It runs inside the outbox dispatch transaction, so it commits exactly when the
/// delivery mark does.
/// </summary>
/// <remarks>
/// Idempotent on its own as well (EV-4, EV-5): an outcome applies only to the revision under validation while the concern is
/// PENDING_VALIDATION, and applying it leaves that state, so the same outcome again, or one for an older revision, is audited as
/// ignored.
/// </remarks>
internal sealed class ConcernValidationOutcomeHandler(
    IManagementConcernRepository repository, IProjectFactsReader projects, IAuditTrail audit, TimeProvider timeProvider) : IApprovalOutcomeHandler
{
    public string SubjectModule => ConcernApprovalRouting.SubjectModule;

    public async Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        await using IConcernWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        ConcernEntity concern = await repository.FindAsync(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false)
                                ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no concern.");
        ProjectFacts project = await projects.FindAsync(concern.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Concern {concern.Id} names no project.");

        ConcernStatus from = concern.Status;
        if (from != ConcernStatus.PendingValidation || concern.RevisionNo != outcome.Subject.RevisionNo)
        {
            audit.Stage(ConcernAudit.OutcomeIgnored(project, concern, outcome));
        }
        else
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            bool validated = outcome.Data.Decision == ApprovalOutcomeDecision.Approved;
            concern.Status = validated ? ConcernStatus.Resolved : ConcernStatus.InProgress;
            concern.ResolvedAt = validated ? now : null;
            concern.RevisionNo += validated ? 0 : 1;
            ConcernGate.Touch(concern, outcome.Data.DecidedByUserId, now);
            audit.Stage(ConcernAudit.Validated(
                validated ? ConcernAuditEvents.ResolutionValidated : ConcernAuditEvents.ResolutionReturned, project, from, concern, outcome));
        }

        // Throwing rolls the dispatch back, and the outcome is delivered again later (TASK-035 D-8).
        ConcernSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved != ConcernSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} could not be applied: {saved}.");
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
