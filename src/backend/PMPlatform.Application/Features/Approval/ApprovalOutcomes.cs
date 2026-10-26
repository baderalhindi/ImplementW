using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval;

/// <summary>
/// Ends a run: its terminal state, the completion audit event, and <see cref="ApprovalOutcomeRecorded"/> in the outbox,
/// all staged into the unit of work that saves the deciding change. The outbox's unique message key is the run's
/// outcome key, so a run can publish one outcome only.
/// </summary>
internal sealed class ApprovalOutcomes(IOutbox outbox, IAuditTrail audit, IAuditRequestContext request)
{
    public const string SourceModule = "Approval";

    public void Complete(ApprovalInstance instance, ApprovalOutcomeDecision decision, Guid actorId, AuditActorType actorType, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);

        instance.Status = decision switch
        {
            ApprovalOutcomeDecision.Approved => ApprovalInstanceStatus.Approved,
            ApprovalOutcomeDecision.Rejected => ApprovalInstanceStatus.Rejected,
            ApprovalOutcomeDecision.Returned => ApprovalInstanceStatus.Returned,
            ApprovalOutcomeDecision.Withdrawn => ApprovalInstanceStatus.Withdrawn,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown outcome."),
        };
        instance.CompletedAt = now;
        audit.Stage(ApprovalAudit.Completed(actorId, actorType, instance));

        outbox.Stage(new ApprovalOutcomeRecorded
        {
            EventId = Guid.CreateVersion7(now),
            EventType = ApprovalOutcomeRecorded.Type,
            SchemaVersion = ApprovalOutcomeRecorded.CurrentSchemaVersion,
            Kind = EventKind.DomainEvent,
            MessageKey = EventMessageKey.Of(ApprovalOutcomeRecorded.Type, instance.OutcomeIdempotencyKey),
            IdempotencyKey = instance.OutcomeIdempotencyKey,
            OccurredAt = now,
            SourceModule = SourceModule,
            CorrelationId = request.CorrelationId,
            Actor = new EventActor(actorType, actorId),
            Subject = new EventSubject(instance.SubjectModule, instance.SubjectType, instance.SubjectId, instance.SubjectRevisionNo),
            Scope = new EventScope(instance.ScopeProjectId, instance.ScopeDepartmentId, null),
            Data = new ApprovalOutcomeData(instance.Id, instance.RoutingKey, decision, now, actorId, instance.AuthorityConfigurationVersionId),
        });
    }

    /// <summary>The run's outcome key (event-conventions EV-4; the sample's form): fixed when the run starts.</summary>
    public static string OutcomeKeyOf(Guid instanceId) => $"apr-{instanceId:D}-outcome";
}
