using PMPlatform.Domain.Common;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// One revision of a period's progress, with actual and planned side by side (ADR-009). <see cref="ActualPercent"/> is
/// the reported figure: the override when <see cref="IsOverridden"/>, else the calculated one, which is always kept.
/// <see cref="ProjectIntakeId"/> marks the ADR-014 opening position.
/// </summary>
public sealed record ProgressSubmissionDetail(
    Guid Id,
    Guid ProjectId,
    Guid ReportingCycleId,
    int RevisionNo,
    ProgressSubmissionStatus Status,
    decimal ActualPercent,
    decimal ActualPercentCalculated,
    decimal? ActualPercentOverride,
    NarrativeText? OverrideReason,
    bool IsOverridden,
    decimal? PlannedPercent,
    Guid? BaselineId,
    NarrativeText? Narrative,
    Guid? ProjectIntakeId,
    Guid? SubmittedByUserId,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    NarrativeText? ReturnReason,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
