using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>An approval run with its whole history (SCR-115): every task, in stage order, including escalated and cancelled ones.</summary>
public sealed record ApprovalInstanceDetail(
    Guid Id,
    ApprovalSubject Subject,
    string RoutingKey,
    Guid AuthorityConfigurationVersionId,
    Guid? ScopeProjectId,
    Guid? ScopeDepartmentId,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAt,
    ApprovalInstanceStatus Status,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? OutcomeDeliveredAt,
    Guid? PreviousInstanceId,
    IReadOnlyList<ApprovalTaskDetail> Tasks);
