using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Schedule;

/// <summary>
/// A baseline of a project's schedule (TASK-046). An APPROVED baseline is a candidate prepared from the working schedule
/// and made ACTIVE through WF-11, or directly where the governance profile requires no approval (ADR-015); its activity
/// dates are copied into <see cref="BaselineActivity"/> as it activates. A DECLARED baseline is the legacy-intake
/// baseline (ADR-014), born ACTIVE with an end date and no activities. At most one baseline of a project is ACTIVE: a
/// partial unique index holds it, and activating one supersedes the other in the same transaction. Delete policy: HARD_DRAFT.
/// </summary>
public sealed class ProjectBaseline : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public BaselineType BaselineType { get; set; }

    /// <summary>Unique per project, in order of creation.</summary>
    public int VersionNo { get; set; }

    /// <summary>A RETURNED candidate is resubmitted as revision + 1, which a new WF-11 run reviews.</summary>
    public int RevisionNo { get; set; } = 1;

    public ProjectBaselineStatus Status { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? SupersededAt { get; set; }

    public Guid? SupersededByBaselineId { get; set; }

    /// <summary>The WF-08 authorisation of a rebaseline (TASK-060); required once an APPROVED baseline is ACTIVE.</summary>
    public Guid? ChangeAuthorizationId { get; set; }

    /// <summary>DECLARED only: the intake that declared it.</summary>
    public Guid? ProjectIntakeId { get; set; }

    /// <summary>DECLARED only: the end date as stated at intake.</summary>
    public DateOnly? DeclaredEndDate { get; set; }

    public NarrativeText? DeclaredScope { get; set; }

    /// <summary>The project finish per this baseline: its latest activity finish, or the declared end date.</summary>
    public DateOnly BaselineFinishDate { get; set; }
}
