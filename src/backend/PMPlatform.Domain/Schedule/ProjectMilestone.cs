using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Schedule;

/// <summary>
/// The single shared milestone identity (ICD-04, TASK-050): one row per milestone, which WF-03 and WF-05 both name and
/// neither copies. WF-03 owns it — its place in the schedule, its title, category and Current Forecast date, and its
/// status. WF-05 owns the achievement: its evidence and the accepted Actual Achievement Date live in
/// <c>milestone.milestone_achievement</c>, never here. Delete policy: RETAIN — a mistake is cancelled, never deleted.
/// </summary>
public sealed class ProjectMilestone : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public Guid ProjectScheduleId { get; set; }

    /// <summary>The activity the milestone completes, where one exists: a live activity of the same schedule.</summary>
    public Guid? ScheduleActivityId { get; set; }

    public required NarrativeText Title { get; set; }

    /// <summary>A PUBLISHED MILESTONE_CATEGORY item; it decides the mandatory evidence of an achievement (EVIDENCE_POLICY).</summary>
    public Guid MilestoneCategoryItemId { get; set; }

    /// <summary>The Current Forecast; a baseline copies it as the milestone's planned date when it activates.</summary>
    public DateOnly ForecastDate { get; set; }

    public ProjectMilestoneStatus Status { get; set; }

    public int SortOrder { get; set; }
}
