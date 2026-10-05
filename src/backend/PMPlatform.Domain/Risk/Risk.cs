using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Risk;

/// <summary>
/// A risk register entry of a project (WF-06, TASK-055). Its rating is never stored here: it is the rating of its latest
/// <see cref="RiskAssessmentVersion"/>, pinned to the matrix version in force when that assessment was made. Materialisation
/// into an issue is recorded on the issue (<c>management_concern.originating_risk_id</c>, edge 15); this row keeps only when it
/// happened. Delete policy: RETAIN — a risk that no longer applies is closed.
/// </summary>
public sealed class Risk : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public required NarrativeText Title { get; set; }

    public required NarrativeText Description { get; set; }

    /// <summary>A RISK_CATEGORY master data item.</summary>
    public Guid RiskCategoryItemId { get; set; }

    public Guid? OwnerUserId { get; set; }

    public RiskStatus Status { get; set; }

    public DateOnly IdentifiedDate { get; set; }

    /// <summary>When the risk is next due for review; an acceptance sets it to its expiry.</summary>
    public DateOnly? NextReviewDate { get; set; }

    /// <summary>Set once, when an issue is raised from this risk.</summary>
    public DateTimeOffset? MaterialisedAt { get; set; }

    /// <summary>Why the risk was closed; set exactly while it is CLOSED.</summary>
    public NarrativeText? ClosureRationale { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public Guid? ClosedByUserId { get; set; }

    /// <summary>How many times the risk was reopened after closure, each by the holder of the reopen permission.</summary>
    public int ReopenedCount { get; set; }
}
