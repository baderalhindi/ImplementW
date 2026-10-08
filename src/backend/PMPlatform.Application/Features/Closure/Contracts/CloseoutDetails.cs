using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Closure.Contracts;

/// <summary>
/// A completion case with its readiness. Approved and effected are different states: an APPROVED case has changed no project yet; the
/// project is COMPLETED once the case is EFFECTED, and <see cref="ActualProjectCompletionDate"/> is then its official completion date.
/// </summary>
public sealed record CompletionCaseDetail(
    Guid Id,
    Guid ProjectId,
    CloseoutCaseStatus Status,
    int RevisionNo,
    Guid RequestedByUserId,
    DateTimeOffset? SubmittedAt,
    DateOnly? ActualProjectCompletionDate,
    NarrativeText? CompletionNarrative,
    DateTimeOffset? EffectedAt,
    ReadinessDetail Readiness,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>
/// A closure case with its readiness. <see cref="Outcome"/> is TERMINATED_WITHOUT_COMPLETION on the terminal path from SUSPENDED, where
/// <see cref="CompletionCaseId"/> is null: such a project has no actual completion date and is never reported as completed.
/// </summary>
public sealed record ClosureCaseDetail(
    Guid Id,
    Guid ProjectId,
    Guid? CompletionCaseId,
    ProjectOutcome Outcome,
    CloseoutCaseStatus Status,
    int RevisionNo,
    Guid RequestedByUserId,
    DateTimeOffset? SubmittedAt,
    NarrativeText? ClosureNarrative,
    DateTimeOffset? EffectedAt,
    ReadinessDetail Readiness,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>
/// The backend's readiness roll-up (WF-10 §6.3, CLO-CC-14) of the case's latest evaluation: READY when every criterion passed,
/// READY_WITH_CONDITIONS when the failed ones are all waived, NOT_READY when one failed and is not waived, INCOMPLETE before any
/// evaluation. While the case is under review, the latest evaluation is the one its submission made: the frozen snapshot.
/// </summary>
public sealed record ReadinessDetail(ReadinessStatus Status, DateTimeOffset? EvaluatedAt, IReadOnlyList<ReadinessCheckDetail> Checks);

/// <summary>
/// One criterion as last evaluated: <see cref="BlockingCount"/> records kept it from passing; a failed waivable criterion is WAIVED by the
/// case's latest waiver of it, whose author and reason are given.
/// </summary>
public sealed record ReadinessCheckDetail(
    ReadinessCheckCode CheckCode,
    ReadinessResult Result,
    int BlockingCount,
    bool Waivable,
    Guid? WaivedByUserId,
    NarrativeText? WaiverReason);

/// <summary>WF-10 §6.3's roll-up statuses.</summary>
public enum ReadinessStatus
{
    Ready = 1,
    ReadyWithConditions = 2,
    NotReady = 3,
    Incomplete = 4,
}

/// <summary>
/// One readiness record as stored (APPEND_ONLY): an evaluated criterion (PASS or FAIL), or a waiver (WAIVED, with its author and reason).
/// A case's history is its records, newest first; the evaluation a submission made is the revision's frozen snapshot.
/// </summary>
public sealed record ReadinessRecordDetail(
    Guid Id,
    Guid? CompletionCaseId,
    Guid? ClosureCaseId,
    ReadinessCheckCode CheckCode,
    ReadinessResult Result,
    DateTimeOffset EvaluatedAt,
    int BlockingCount,
    Guid? WaivedByUserId,
    NarrativeText? WaiverReason,
    Guid CreatedBy);

/// <summary>A post-project obligation: open while OPEN or IN_PROGRESS; SATISFIED, WAIVED and CANCELLED are settled.</summary>
public sealed record PostProjectObligationDetail(
    Guid Id,
    Guid ProjectId,
    Guid? CompletionCaseId,
    Guid? ClosureCaseId,
    NarrativeText Title,
    NarrativeText? Description,
    Guid? OwnerUserId,
    DateOnly? DueDate,
    PostProjectObligationStatus Status,
    DateTimeOffset? SatisfiedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
