using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Schedule;

/// <summary>
/// A directed dependency between two leaf activities of one working schedule (TASK-046). The graph is acyclic: a
/// dependency that would close a cycle is refused at save, by the service and by the database. A dependency is never
/// changed, only removed and added again. Delete policy: HARD_WORKING.
/// </summary>
public sealed class ScheduleDependency : AuditedEntity
{
    public Guid PredecessorActivityId { get; set; }

    public Guid SuccessorActivityId { get; set; }

    public ScheduleDependencyType DependencyType { get; set; }

    /// <summary>Non-negative working days (the spec's BR-SCH-025).</summary>
    public int LagDays { get; set; }
}
