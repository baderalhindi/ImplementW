using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// One stage assignment. <see cref="AssignedUserId"/> is whose authority decided it and <see cref="ActingUserId"/> who
/// acted; they differ only under <see cref="ApprovalDelegationId"/>. A task of a later stage has no <see cref="DueAt"/>
/// until its stage is reached.
/// </summary>
public sealed record ApprovalTaskDetail(
    Guid Id,
    short SequenceNo,
    Guid AssignedRoleId,
    Guid? AssignedUserId,
    Guid? ActingUserId,
    Guid? ApprovalDelegationId,
    ApprovalTaskStatus Status,
    DateTimeOffset? DueAt,
    DateTimeOffset? DecidedAt,
    NarrativeText? DecisionReason,
    Guid? EscalatedToTaskId,
    DateTimeOffset CreatedAt);
