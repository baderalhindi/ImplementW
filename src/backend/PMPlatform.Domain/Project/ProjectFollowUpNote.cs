using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Project;

/// <summary>
/// A dated follow-up note on a project, attributed to who wrote it (RPMO register column "ملاحظات", R3-3): its creation stamp is its date and
/// author. Delete policy: APPEND_ONLY.
/// </summary>
public sealed class ProjectFollowUpNote : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public required NarrativeText Note { get; set; }
}
