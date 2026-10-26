using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval;

internal static class ApprovalMapping
{
    public static ApprovalSubject SubjectOf(ApprovalInstance instance) =>
        new(instance.SubjectModule, instance.SubjectType, instance.SubjectId, instance.SubjectRevisionNo);

    public static ApprovalInstanceSummary ToSummary(ApprovalInstance instance) =>
        new(instance.Id, SubjectOf(instance), instance.RoutingKey, instance.RequestedByUserId, instance.RequestedAt, instance.Status, instance.CompletedAt);

    public static ApprovalInstanceDetail ToDetail(ApprovalInstance instance, IEnumerable<ApprovalTask> tasks) =>
        new(
            instance.Id,
            SubjectOf(instance),
            instance.RoutingKey,
            instance.AuthorityConfigurationVersionId,
            instance.ScopeProjectId,
            instance.ScopeDepartmentId,
            instance.RequestedByUserId,
            instance.RequestedAt,
            instance.Status,
            instance.CompletedAt,
            instance.OutcomeDeliveredAt,
            instance.PreviousInstanceId,
            [.. tasks.OrderBy(t => t.SequenceNo).ThenBy(t => t.CreatedAt).ThenBy(t => t.Id).Select(ToDetail)]);

    public static ApprovalTaskDetail ToDetail(ApprovalTask task) =>
        new(
            task.Id,
            task.SequenceNo,
            task.AssignedRoleId,
            task.AssignedUserId,
            task.ActingUserId,
            task.ApprovalDelegationId,
            task.Status,
            task.DueAt,
            task.DecidedAt,
            task.DecisionReason,
            task.EscalatedToTaskId,
            task.CreatedAt);

    public static ApprovalDelegationDetail ToDetail(ApprovalDelegation delegation) =>
        new(
            delegation.Id,
            delegation.DelegatorUserId,
            delegation.DelegateUserId,
            delegation.RoutingKey,
            delegation.ValidFrom,
            delegation.ValidTo,
            delegation.Status,
            delegation.RevokedAt);
}
