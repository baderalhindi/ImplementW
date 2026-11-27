namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// A task the caller may decide now (SCR-100): PENDING, in its run's current stage, and within the caller's authority
/// or a delegator's. <see cref="OnBehalfOfUserId"/> names the delegator when the authority is delegated. <see cref="Project"/> names the
/// project the run is scoped to, which that authority covers; null when the run has no project scope (stage QA D1-01).
/// </summary>
public sealed record ApprovalInboxItem(
    Guid TaskId,
    short SequenceNo,
    Guid AssignedRoleId,
    DateTimeOffset? DueAt,
    Guid? OnBehalfOfUserId,
    ApprovalInstanceSummary Instance,
    ApprovalProjectName? Project = null);
