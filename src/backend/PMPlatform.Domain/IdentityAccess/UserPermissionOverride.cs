using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// One permission adjusted for one user on top of their roles (client review, item 5). At most one per user and permission; going
/// back to what the roles give removes the row, and the audit trail keeps the history. Delete policy: RETAIN the user, CASCADE nothing.
/// </summary>
public sealed class UserPermissionOverride : AuditedEntity
{
    public Guid UserId { get; set; }

    public Guid PermissionId { get; set; }

    public PermissionOverrideEffect Effect { get; set; }

    /// <summary>The scope a <see cref="PermissionOverrideEffect.Grant"/> is held at; null on a removal.</summary>
    public DataScope? DataScope { get; set; }

    public required NarrativeText Reason { get; set; }
}
