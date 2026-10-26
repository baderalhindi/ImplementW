using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// Binds a user to a permission profile version with its scope anchors. A per-project external grant (ADR-013)
/// carries <see cref="ProjectId"/>, <see cref="ExternalEntityId"/> and a named <see cref="SponsorUserId"/>, and
/// ends on project closure or role change (TASK-031). Delete policy: RETAIN.
/// </summary>
public sealed class AccessRelationship : AuditedEntity
{
    public Guid UserId { get; set; }

    /// <summary>The assignment stays on this version until it is explicitly migrated (TASK-110).</summary>
    public Guid PermissionProfileVersionId { get; set; }

    /// <summary>DEPT scope anchor.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>ENTITY scope anchor.</summary>
    public Guid? ExternalEntityId { get; set; }

    /// <summary>Per-project grant anchor, e.g. an entity Project Manager (ADR-013).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The named AHDA sponsor; required for external grants.</summary>
    public Guid? SponsorUserId { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public AccessEndReason? EndReason { get; set; }

    public AccessRelationshipStatus Status { get; set; }
}
