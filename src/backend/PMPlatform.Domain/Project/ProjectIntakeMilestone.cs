using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Project;

/// <summary>A milestone declared at intake as already achieved; WF-05 records the achievement from the event. Delete policy: APPEND_ONLY.</summary>
public sealed class ProjectIntakeMilestone : AuditedEntity
{
    public Guid ProjectIntakeId { get; set; }

    public required NarrativeText Title { get; set; }

    public DateOnly AchievedDate { get; set; }
}
