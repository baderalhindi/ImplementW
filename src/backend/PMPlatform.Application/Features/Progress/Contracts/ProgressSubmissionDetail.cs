using PMPlatform.Domain.Common;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// One revision of a period's progress, with actual and planned side by side (ADR-009). <see cref="ActualPercent"/> is
/// the reported figure: the override when <see cref="IsOverridden"/>, else the calculated one, which is always kept.
/// <see cref="ProjectIntakeId"/> marks the ADR-014 opening position. A stage-based period (review round 7, item 1) has its <see cref="Stages"/>,
/// weighed by the PROJECT_STAGE version <see cref="StageVersionNo"/>, and names its <see cref="CurrentStageCode"/>.
/// </summary>
public sealed record ProgressSubmissionDetail(
    Guid Id,
    Guid ProjectId,
    Guid ReportingCycleId,
    int RevisionNo,
    ProgressSubmissionStatus Status,
    decimal ActualPercent,
    decimal ActualPercentCalculated,
    ProgressActualSource ActualSource,
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
    Guid UpdatedBy)
{
    public Guid? StageConfigurationVersionId { get; init; }

    public int? StageVersionNo { get; init; }

    public string? CurrentStageCode { get; init; }

    public IReadOnlyList<ProgressStageDetail> Stages { get; init; } = [];
}

/// <summary>
/// One stage of a period: its name and weight in the version the period weighs by, whether it applies, the planned % on the period's date, the % complete
/// and the last published period's (<see cref="PreviousPercent"/>, below which a decrease needs a reason), and whether the schedule measures it.
/// </summary>
public sealed record ProgressStageDetail(
    string StageCode,
    BilingualLabel? Name,
    short SortOrder,
    decimal WeightPercent,
    bool Applies,
    decimal? PlannedPercent,
    decimal? PercentComplete,
    decimal? PreviousPercent,
    bool IsFromSchedule,
    DateOnly? ActualStartDate,
    DateOnly? ActualFinishDate,
    string? Note,
    NarrativeText? DecreaseReason);
