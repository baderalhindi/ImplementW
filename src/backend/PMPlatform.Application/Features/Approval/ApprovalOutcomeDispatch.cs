using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval;

/// <summary>
/// Delivers <see cref="ApprovalOutcomeRecorded"/> to the source module the subject names (EV-11), and records on the run
/// that it was delivered. The outbox dispatcher runs this in the transaction that marks the message dispatched, so the
/// source's change, the run's delivery mark and the dispatch mark commit together, once.
/// </summary>
internal sealed class ApprovalOutcomeDispatch(
    IEnumerable<IApprovalOutcomeHandler> handlers, IApprovalRepository repository, IAuditTrail audit, TimeProvider timeProvider)
    : IDomainEventConsumer
{
    public string EventType => ApprovalOutcomeRecorded.Type;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        ApprovalOutcomeRecorded outcome = EventSerialization.Deserialize<ApprovalOutcomeRecorded>(payload);
        ApprovalInstance instance = await repository.FindInstanceAsync(outcome.Data.ApprovalInstanceId, cancellationToken).ConfigureAwait(false)
                                    ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no run.");
        if (instance.OutcomeDeliveredAt is not null)
        {
            return;
        }

        IApprovalOutcomeHandler handler = handlers.SingleOrDefault(h => string.Equals(h.SubjectModule, outcome.Subject.Module, StringComparison.Ordinal))
                                          ?? throw new InvalidOperationException($"No approval outcome handler is registered for module {outcome.Subject.Module}.");
        await handler.HandleAsync(outcome, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = timeProvider.GetUtcNow();
        instance.OutcomeDeliveredAt = now;
        ApprovalRows.Touch(instance, ApprovalServicePrincipal.Id, now);
        audit.Stage(ApprovalAudit.Delivered(instance));
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != ApprovalSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} could not be marked delivered.");
        }
    }
}
