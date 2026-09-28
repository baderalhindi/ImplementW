using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval;

/// <summary>The audit events of the approval runtime (TASK-033 <c>IAuditTrail</c>): every decision, transition, refusal and delegation.</summary>
internal static class ApprovalAudit
{
    public const string Module = "Approval";

    public static AuditEntry Requested(ApprovalInstance instance) =>
        RunEntry(AuditEventClass.LifecycleTransition, ApprovalAuditEvents.ApprovalRequested, instance.RequestedByUserId, AuditActorType.User, instance, []);

    public static AuditEntry Decided(string eventType, Guid actorId, ApprovalInstance instance, ApprovalTask task) =>
        new(AuditEventClass.ApprovalDecision, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(ApprovalTask), task.Id),
            ScopeProjectId = instance.ScopeProjectId,
            Attributes =
            [
                .. RunAttributes(instance),
                AuditAttribute.Of(ApprovalAuditAttributes.SequenceNo, task.SequenceNo),
                AuditAttribute.Of(ApprovalAuditAttributes.AssignedRoleId, task.AssignedRoleId),
                AuditAttribute.Of(ApprovalAuditAttributes.AuthorityUserId, task.AssignedUserId),
                .. task.ApprovalDelegationId is { } delegation ? [AuditAttribute.Of(ApprovalAuditAttributes.ApprovalDelegationId, delegation)] : Array.Empty<AuditAttribute>(),
            ],
        };

    public static AuditEntry Escalated(Guid actorId, AuditActorType actorType, ApprovalInstance instance, ApprovalTask task) =>
        new(AuditEventClass.LifecycleTransition, ApprovalAuditEvents.TaskEscalated, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            ActorType = actorType,
            Subject = new AuditSubject(Module, nameof(ApprovalTask), task.Id),
            ScopeProjectId = instance.ScopeProjectId,
            Attributes =
            [
                .. RunAttributes(instance),
                AuditAttribute.Of(ApprovalAuditAttributes.SequenceNo, task.SequenceNo),
                AuditAttribute.Of(ApprovalAuditAttributes.AssignedRoleId, task.AssignedRoleId),
                AuditAttribute.Of(ApprovalAuditAttributes.EscalatedToTaskId, task.EscalatedToTaskId),
            ],
        };

    public static AuditEntry Completed(Guid actorId, AuditActorType actorType, ApprovalInstance instance) =>
        RunEntry(AuditEventClass.LifecycleTransition, ApprovalAuditEvents.ApprovalCompleted, actorId, actorType, instance,
            [AuditAttribute.Of(ApprovalAuditAttributes.Status, instance.Status)]);

    public static AuditEntry Delivered(ApprovalInstance instance) =>
        RunEntry(AuditEventClass.LifecycleTransition, ApprovalAuditEvents.OutcomeDelivered, ApprovalServicePrincipal.Id, AuditActorType.Service, instance,
            [AuditAttribute.Of(ApprovalAuditAttributes.Status, instance.Status)]);

    /// <summary>An attempt on a run the actor has no authority over at that moment (CTL-25); the reason is recorded, not returned.</summary>
    public static AuditEntry Refused(Guid actorId, string attempted, ApprovalInstance instance, ApprovalTask? task, ApprovalRefusal refusal) =>
        new(AuditEventClass.AuthorizationDenial, ApprovalAuditEvents.DecisionRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = task is null ? new AuditSubject(Module, nameof(ApprovalInstance), instance.Id) : new AuditSubject(Module, nameof(ApprovalTask), task.Id),
            ScopeProjectId = instance.ScopeProjectId,
            Attributes =
            [
                .. RunAttributes(instance),
                AuditAttribute.Of(ApprovalAuditAttributes.Decision, attempted),
                AuditAttribute.Of(ApprovalAuditAttributes.RefusalReason, refusal),
            ],
        };

    public static AuditEntry Delegation(string eventType, Guid actorId, AuditActorType actorType, ApprovalDelegation delegation) =>
        new(AuditEventClass.PermissionChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            ActorType = actorType,
            Subject = new AuditSubject(Module, nameof(ApprovalDelegation), delegation.Id),
            Attributes =
            [
                AuditAttribute.Of(ApprovalAuditAttributes.DelegateUserId, delegation.DelegateUserId),
                AuditAttribute.Of(ApprovalAuditAttributes.RoutingKey, delegation.RoutingKey),
                AuditAttribute.Of(ApprovalAuditAttributes.ValidFrom, delegation.ValidFrom),
                AuditAttribute.Of(ApprovalAuditAttributes.ValidTo, delegation.ValidTo),
                AuditAttribute.Of(ApprovalAuditAttributes.Status, delegation.Status),
            ],
        };

    private static AuditEntry RunEntry(
        AuditEventClass eventClass, string eventType, Guid actorId, AuditActorType actorType, ApprovalInstance instance, IEnumerable<AuditAttribute> extra) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            ActorType = actorType,
            Subject = new AuditSubject(Module, nameof(ApprovalInstance), instance.Id),
            ScopeProjectId = instance.ScopeProjectId,
            Attributes = [.. RunAttributes(instance), .. extra],
        };

    private static IEnumerable<AuditAttribute> RunAttributes(ApprovalInstance instance) =>
    [
        AuditAttribute.Of(ApprovalAuditAttributes.ApprovalInstanceId, instance.Id),
        AuditAttribute.Of(ApprovalAuditAttributes.SubjectModule, instance.SubjectModule),
        AuditAttribute.Of(ApprovalAuditAttributes.SubjectType, instance.SubjectType),
        AuditAttribute.Of(ApprovalAuditAttributes.SubjectId, instance.SubjectId),
        AuditAttribute.Of(ApprovalAuditAttributes.SubjectRevisionNo, instance.SubjectRevisionNo),
        AuditAttribute.Of(ApprovalAuditAttributes.RoutingKey, instance.RoutingKey),
        AuditAttribute.Of(ApprovalAuditAttributes.AuthorityConfigurationVersionId, instance.AuthorityConfigurationVersionId),
    ];
}
