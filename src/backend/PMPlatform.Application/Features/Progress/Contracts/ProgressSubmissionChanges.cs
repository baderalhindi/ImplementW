using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// What a person writes on a DRAFT. There is no actual or planned figure: both are derived (ADR-009). A project-level
/// actual is entered only as an override, with its reason. <see cref="Stages"/>, on a stage-based period, is each stage's % complete
/// and dates (review round 7, item 1); null leaves them as they are.
/// </summary>
public sealed record ProgressSubmissionChanges(NarrativeText? Narrative, ProgressOverride? Override, IReadOnlyList<ProgressStageChange>? Stages = null);

/// <summary>
/// One stage as entered: its % complete, 0 to 100, and when it actually started and finished, with an optional one-line note. A % lower than the
/// last published period's needs <see cref="DecreaseReason"/>. A stage the schedule measures takes no % here.
/// </summary>
public sealed record ProgressStageChange(
    string StageCode, decimal? PercentComplete, DateOnly? ActualStartDate, DateOnly? ActualFinishDate, string? Note, NarrativeText? DecreaseReason);

/// <summary>ADR-009: an override of the project roll-up, 0–100, with the reason recorded; the calculated value is kept.</summary>
public sealed record ProgressOverride(decimal ActualPercent, NarrativeText Reason);
