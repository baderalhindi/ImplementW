using PMPlatform.Domain.Common;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension.Contracts;

/// <summary>
/// A suspension or resumption request with the suspension period its effect opened or ended. Approved and effected are different
/// states: an APPROVED request has changed no project yet, and <see cref="Suspension"/> stays null until it is EFFECTED.
/// </summary>
public sealed record SuspensionRequestDetail(
    Guid Id,
    Guid ProjectId,
    SuspensionRequestType RequestType,
    SuspensionRequestStatus Status,
    int RevisionNo,
    NarrativeText Reason,
    Guid RequestedByUserId,
    DateTimeOffset? SubmittedAt,
    DateOnly? RequestedEffectiveDate,
    DateOnly? PlannedResumptionDate,
    DateTimeOffset? EffectedAt,
    ActiveSuspensionDetail? Suspension,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>
/// A period during which the project was or is SUSPENDED: open while <see cref="EndedAt"/> is null. It ends when the project resumes, or
/// when it is closed without resuming (<see cref="SuspensionEndReason.ProjectClosed"/>, TASK-063).
/// </summary>
public sealed record ActiveSuspensionDetail(
    Guid Id,
    Guid ProjectId,
    Guid SuspensionRequestId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    SuspensionEndReason? EndReason,
    Guid? ResumptionRequestId);
