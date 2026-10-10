using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Dashboards;

/// <summary>
/// One user's presentation choices on one PUBLISHED dashboard version (ADR-019, FG-01 §14.1): which optional widgets are hidden
/// and in what order. It never changes what a widget presents or who may see it (DSH-CC-29). A new version starts from the
/// governed layout. Delete policy: HARD_OWNER — a reset deletes it.
/// </summary>
public sealed class UserDashboardPreference : AuditedEntity
{
    public Guid UserId { get; set; }

    public Guid DashboardDefinitionId { get; set; }
}
