using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Progress;

/// <summary>
/// One stage of a period's progress (review round 7, item 1). The weight is copied from the PROJECT_STAGE version the period weighs by, and the planned
/// % from the project's stage plan on the period's date, so a published period's figures never move with a later version or plan. The % complete is
/// entered, except Execution's while an active baseline measures it (<see cref="IsFromSchedule"/>). Delete policy: HARD_DRAFT, with its submission.
/// </summary>
public sealed class ProgressStageEntry : AuditedEntity
{
    public Guid ProgressSubmissionId { get; set; }

    public required string StageCode { get; set; }

    public short SortOrder { get; set; }

    public decimal WeightPercent { get; set; }

    /// <summary>As the stage plan says; a stage that does not apply takes its weight out of the overall figure.</summary>
    public bool Applies { get; set; }

    /// <summary>0 before the stage's planned start, 100 from its planned end, linear between; null when the plan has no dates for it.</summary>
    public decimal? PlannedPercent { get; set; }

    /// <summary>0 to 100; null until entered.</summary>
    public decimal? PercentComplete { get; set; }

    /// <summary>The % complete is the schedule's roll-up, not typed in: Execution, while the project has an active baseline.</summary>
    public bool IsFromSchedule { get; set; }

    /// <summary>Required once the stage has started.</summary>
    public DateOnly? ActualStartDate { get; set; }

    /// <summary>Required once the stage reaches 100%.</summary>
    public DateOnly? ActualFinishDate { get; set; }

    /// <summary>An optional one-line note.</summary>
    public string? Note { get; set; }

    /// <summary>Why the % complete is lower than the last published period's; required then, absent otherwise.</summary>
    public NarrativeText? DecreaseReason { get; set; }
}
