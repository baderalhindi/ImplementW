using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Suspension;
using A = PMPlatform.Application.Features.Suspension.Contracts.Events.SuspensionAuditAttributes;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// The audit events of WF-09 (TASK-062 <c>IAuditTrail</c>). Every event has the request as subject and carries its type, so a request's
/// history — raised, decided, effected — is one query. The reason is free text and is not copied.
/// </summary>
internal static class SuspensionAudit
{
    public static AuditEntry Created(Guid actorId, ProjectFacts project, SuspensionRequest request) =>
        Entry(AuditEventClass.DataChange, SuspensionAuditEvents.RequestCreated, actorId, project, request,
        [
            AuditAttribute.Of(A.Status, request.Status),
            AuditAttribute.Of(A.Reason, AuditAttribute.Withheld),
            .. Dates(null, request),
        ]);

    public static AuditEntry Changed(Guid actorId, ProjectFacts project, SuspensionRequestChanges before, SuspensionRequest request)
    {
        ArgumentNullException.ThrowIfNull(before);
        return Entry(AuditEventClass.DataChange, SuspensionAuditEvents.RequestChanged, actorId, project, request,
            new[] { AuditAttribute.WithheldChange(A.Reason, before.Reason.Text, request.Reason.Text) }.OfType<AuditAttribute>().Concat(Dates(before, request)));
    }

    public static AuditEntry Deleted(Guid actorId, ProjectFacts project, SuspensionRequest request) =>
        Entry(AuditEventClass.DataChange, SuspensionAuditEvents.RequestDeleted, actorId, project, request, [AuditAttribute.Of(A.Status, request.Status)]);

    /// <summary>A move along <see cref="SuspensionWorkflow"/>, with the revision and, for a review, its WF-11 run.</summary>
    public static AuditEntry Transition(
        string eventType, Guid actorId, ProjectFacts project, SuspensionRequestStatus from, SuspensionRequest request, Guid? approvalInstanceId = null) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, request,
            new[]
            {
                AuditAttribute.Change(A.Status, from, request.Status) ?? AuditAttribute.Of(A.Status, request.Status),
                AuditAttribute.Of(A.RevisionNo, request.RevisionNo),
                approvalInstanceId is null ? null : AuditAttribute.Of(A.ApprovalInstanceId, approvalInstanceId),
            }.OfType<AuditAttribute>());

    /// <summary>WF-11's decision on a revision, applied: the decider is the actor. APPROVED changes no project.</summary>
    public static AuditEntry Decided(string eventType, ProjectFacts project, SuspensionRequestStatus from, SuspensionRequest request, ApprovalOutcomeRecorded outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        AuditEntry entry = Transition(eventType, outcome.Data.DecidedByUserId, project, from, request, outcome.Data.ApprovalInstanceId);
        return entry with { Attributes = [.. entry.Attributes, AuditAttribute.Of(A.ApprovalDecision, outcome.Data.Decision)] };
    }

    /// <summary>
    /// APPROVED → EFFECTED: the lifecycle activation, by a person or by WF-09's service principal, with the suspension period it opened or
    /// ended. Its own event, in its own transaction, apart from the approval.
    /// </summary>
    public static AuditEntry Effected(Guid actorId, AuditActorType actorType, ProjectFacts project, SuspensionRequest request, ActiveSuspension suspension)
    {
        ArgumentNullException.ThrowIfNull(suspension);
        AuditEntry entry = Transition(SuspensionAuditEvents.RequestEffected, actorId, project, SuspensionRequestStatus.Approved, request);
        return entry with
        {
            ActorType = actorType,
            Attributes = [.. entry.Attributes, AuditAttribute.Of(A.ActiveSuspensionId, suspension.Id), AuditAttribute.Of(A.EffectedAt, request.EffectedAt)],
        };
    }

    /// <summary>
    /// The project's open suspension ended as PROJECT_CLOSED by WF-10's terminal closure (TASK-063), by whoever effected the closure case,
    /// with the suspension request that opened the period as subject.
    /// </summary>
    public static AuditEntry EndedByClosure(SuspensionClosureCommand command, ProjectFacts project, ActiveSuspension suspension)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(suspension);
        return new AuditEntry(AuditEventClass.LifecycleTransition, SuspensionAuditEvents.SuspensionEnded, AuditOutcome.Success)
        {
            ActorUserId = command.ActorId,
            ActorType = command.ActorType,
            Subject = new AuditSubject(SuspensionApprovalRouting.SubjectModule, SuspensionApprovalRouting.SubjectType, suspension.SuspensionRequestId),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes =
            [
                AuditAttribute.Of(A.ActiveSuspensionId, suspension.Id),
                AuditAttribute.Of(A.EndReason, suspension.EndReason),
                AuditAttribute.Of(A.ClosureCaseId, command.ClosureCaseId),
            ],
        };
    }

    /// <summary>EV-5: an outcome for a revision no longer under review, recorded and not applied.</summary>
    public static AuditEntry OutcomeIgnored(ProjectFacts project, SuspensionRequest request, ApprovalOutcomeRecorded outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return Entry(AuditEventClass.DataChange, SuspensionAuditEvents.OutcomeIgnored, outcome.Data.DecidedByUserId, project, request,
        [
            AuditAttribute.Of(A.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
            AuditAttribute.Of(A.ApprovalDecision, outcome.Data.Decision),
            AuditAttribute.Of(A.RevisionNo, outcome.Subject.RevisionNo),
            AuditAttribute.Of(A.Status, request.Status),
        ]);
    }

    /// <summary>ADR-013: review or activation refused to an external user, whatever they hold.</summary>
    public static AuditEntry AuthorityRefused(Guid actorId, ProjectFacts project, Guid suspensionRequestId, string permissionCode, string reason) =>
        new(AuditEventClass.AuthorizationDenial, SuspensionAuditEvents.AuthorityRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(SuspensionApprovalRouting.SubjectModule, SuspensionApprovalRouting.SubjectType, suspensionRequestId),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [AuditAttribute.Of(A.Permission, permissionCode), AuditAttribute.Of(A.RefusalReason, reason)],
        };

    private static IEnumerable<AuditAttribute> Dates(SuspensionRequestChanges? before, SuspensionRequest after) =>
        new[]
        {
            AuditAttribute.Change(A.RequestedEffectiveDate, before?.RequestedEffectiveDate, after.RequestedEffectiveDate),
            AuditAttribute.Change(A.PlannedResumptionDate, before?.PlannedResumptionDate, after.PlannedResumptionDate),
        }.OfType<AuditAttribute>();

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, ProjectFacts project, SuspensionRequest request, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            ActorType = AuditActorType.User,
            Subject = new AuditSubject(SuspensionApprovalRouting.SubjectModule, SuspensionApprovalRouting.SubjectType, request.Id),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [AuditAttribute.Of(A.RequestType, request.RequestType), .. attributes],
        };
}
