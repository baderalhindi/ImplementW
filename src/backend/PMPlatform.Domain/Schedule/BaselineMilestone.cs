using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Schedule;

/// <summary>
/// A milestone's date as its baseline activated: the reference of the milestone's variance, never changed (TASK-050).
/// Delete policy: APPEND_ONLY.
/// </summary>
public sealed class BaselineMilestone : AuditedEntity
{
    public Guid ProjectBaselineId { get; set; }

    public Guid ProjectMilestoneId { get; set; }

    /// <summary>The milestone's forecast date when the baseline activated.</summary>
    public DateOnly PlannedDate { get; set; }
}
