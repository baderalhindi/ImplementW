using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.Closure.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure.EventHandlers;

/// <summary>
/// Applies the outcome of a completion or closure case's WF-11 run (ADR-003 §8.2 edges 27, 28). APPROVED makes the case APPROVED and nothing
/// more: the business decision, audited as <see cref="ClosureAuditEvents.CaseApproved"/>. It calls no Project command, so the project stays
/// as it was until the case is activated, as its own step in its own transaction (WF-10 P3, BR-CLO-004, BR-CLO-005, CLO-CC-10). RETURNED,
/// REJECTED and WITHDRAWN end the run as WF-11 decided it. It runs inside the outbox dispatch transaction, so it commits exactly when the
/// delivery mark does.
/// </summary>
/// <remarks>
/// Idempotent on its own as well (EV-4, EV-5): an outcome applies only to the revision under review while the case is UNDER_REVIEW, and
/// applying it leaves that state, so the same outcome again, or one for an older revision, is audited as ignored.
/// </remarks>
internal sealed class CloseoutApprovalOutcomeHandler(
    ICloseoutRepository repository, IProjectFactsReader projects, IAuditTrail audit, TimeProvider timeProvider) : IApprovalOutcomeHandler
{
    public string SubjectModule => ClosureApprovalRouting.SubjectModule;

    public async Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        CloseoutCase @case = (outcome.Subject.Type switch
        {
            ClosureApprovalRouting.CompletionSubjectType => await repository.FindCaseAsync<CompletionCase>(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false),
            ClosureApprovalRouting.ClosureSubjectType => (CloseoutCase?)await repository.FindCaseAsync<ClosureCase>(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false),
            _ => null,
        }) ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no completion or closure case.");
        ProjectFacts project = await projects.FindAsync(@case.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Case {@case.Id} names no project.");

        CloseoutCaseStatus from = @case.Status;
        if (from != CloseoutCaseStatus.UnderReview || @case.RevisionNo != outcome.Subject.RevisionNo)
        {
            audit.Stage(CloseoutAudit.OutcomeIgnored(project, @case, outcome));
        }
        else
        {
            @case.Status = CloseoutWorkflow.OutcomeOf(outcome.Data.Decision);
            CloseoutGate.Touch(@case, outcome.Data.DecidedByUserId, timeProvider.GetUtcNow());
            audit.Stage(CloseoutAudit.Decided(EventOf(@case.Status), project, from, @case, outcome));
        }

        // Throwing rolls the dispatch back, and the outcome is delivered again later (TASK-035 D-8).
        CloseoutSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved != CloseoutSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} could not be applied: {saved}.");
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string EventOf(CloseoutCaseStatus status) => status switch
    {
        CloseoutCaseStatus.Approved => ClosureAuditEvents.CaseApproved,
        CloseoutCaseStatus.Returned => ClosureAuditEvents.CaseReturned,
        CloseoutCaseStatus.Rejected => ClosureAuditEvents.CaseRejected,
        CloseoutCaseStatus.Withdrawn => ClosureAuditEvents.CaseWithdrawn,
        CloseoutCaseStatus.Draft or CloseoutCaseStatus.Submitted or CloseoutCaseStatus.UnderReview or CloseoutCaseStatus.Effected or _ =>
            throw new ArgumentOutOfRangeException(nameof(status), status, "Not an outcome of a review."),
    };
}
