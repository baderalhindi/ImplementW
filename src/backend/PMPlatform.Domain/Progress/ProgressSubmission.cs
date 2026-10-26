using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Progress;

/// <summary>
/// The working progress record of a reporting period, one row per revision (ADR-009, ADR-014). Actual progress is
/// derived, never typed in: <see cref="ActualPercentCalculated"/> is the duration-weighted roll-up, and the only
/// project-level figure a person enters is an override, which needs a reason and leaves the calculated value in place.
/// <see cref="PlannedPercent"/> is calculated from the active baseline and is never editable. Delete policy: HARD_DRAFT.
/// </summary>
public sealed class ProgressSubmission : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public Guid ReportingCycleId { get; set; }

    /// <summary>A RETURNED revision is followed by revision + 1 in the same period.</summary>
    public int RevisionNo { get; set; } = 1;

    public ProgressSubmissionStatus Status { get; set; }

    /// <summary>The duration-weighted roll-up, recalculated at submission; for the opening position, the intake's figure.</summary>
    public decimal ActualPercentCalculated { get; set; }

    /// <summary>The project-level override; null when the calculated value stands.</summary>
    public decimal? ActualPercentOverride { get; set; }

    /// <summary>Required with an override, absent without one.</summary>
    public NarrativeText? OverrideReason { get; set; }

    /// <summary>From the active baseline at submission; null when there is none, or a Declared Baseline with no plan.</summary>
    public decimal? PlannedPercent { get; set; }

    /// <summary>The baseline <see cref="PlannedPercent"/> was calculated from.</summary>
    public Guid? BaselineId { get; set; }

    public NarrativeText? Narrative { get; set; }

    /// <summary>Set on the opening-position submission only (ADR-014).</summary>
    public Guid? ProjectIntakeId { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public NarrativeText? ReturnReason { get; set; }

    /// <summary>The figure that is reported: the override when there is one, else the calculated value.</summary>
    public decimal EffectiveActualPercent => ActualPercentOverride ?? ActualPercentCalculated;

    public bool IsOverridden => ActualPercentOverride is not null;
}
