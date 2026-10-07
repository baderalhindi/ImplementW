using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts.Events;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension.EventHandlers;

/// <summary>
/// Applies the outcome of a suspension or resumption request's WF-11 run (ADR-003 §8.2 edges 23, 28). APPROVED makes the request
/// APPROVED and nothing more: the business decision, audited as <see cref="SuspensionAuditEvents.RequestApproved"/>. It calls no Project
/// command, so the project stays as it was until the request is activated, as its own step in its own transaction (WF-09 P3, BR-SUS-006).
/// RETURNED, REJECTED and WITHDRAWN end the run as WF-11 decided it. It runs inside the outbox dispatch transaction, so it commits exactly
/// when the delivery mark does.
/// </summary>
/// <remarks>
/// Idempotent on its own as well (EV-4, EV-5): an outcome applies only to the revision under review while the request is UNDER_REVIEW,
/// and applying it leaves that state, so the same outcome again, or one for an older revision, is audited as ignored.
/// </remarks>
internal sealed class SuspensionApprovalOutcomeHandler(
    ISuspensionRepository repository, IProjectFactsReader projects, IAuditTrail audit, TimeProvider timeProvider) : IApprovalOutcomeHandler
{
    public string SubjectModule => SuspensionApprovalRouting.SubjectModule;

    public async Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        await using ISuspensionWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        SuspensionRequest request = await repository.FindAsync(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false)
                                    ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no suspension request.");
        ProjectFacts project = await projects.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Suspension request {request.Id} names no project.");

        SuspensionRequestStatus from = request.Status;
        if (from != SuspensionRequestStatus.UnderReview || request.RevisionNo != outcome.Subject.RevisionNo)
        {
            audit.Stage(SuspensionAudit.OutcomeIgnored(project, request, outcome));
        }
        else
        {
            request.Status = SuspensionWorkflow.OutcomeOf(outcome.Data.Decision);
            SuspensionGate.Touch(request, outcome.Data.DecidedByUserId, timeProvider.GetUtcNow());
            audit.Stage(SuspensionAudit.Decided(EventOf(request.Status), project, from, request, outcome));
        }

        // Throwing rolls the dispatch back, and the outcome is delivered again later (TASK-035 D-8).
        SuspensionSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved != SuspensionSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} could not be applied: {saved}.");
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string EventOf(SuspensionRequestStatus status) => status switch
    {
        SuspensionRequestStatus.Approved => SuspensionAuditEvents.RequestApproved,
        SuspensionRequestStatus.Returned => SuspensionAuditEvents.RequestReturned,
        SuspensionRequestStatus.Rejected => SuspensionAuditEvents.RequestRejected,
        SuspensionRequestStatus.Withdrawn => SuspensionAuditEvents.RequestWithdrawn,
        SuspensionRequestStatus.Draft or SuspensionRequestStatus.Submitted or SuspensionRequestStatus.UnderReview or SuspensionRequestStatus.Effected or _ =>
            throw new ArgumentOutOfRangeException(nameof(status), status, "Not an outcome of a review."),
    };
}
