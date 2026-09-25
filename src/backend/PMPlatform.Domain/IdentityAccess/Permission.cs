using System.Diagnostics.CodeAnalysis;
using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>An entry of the protected permission catalogue (Appendix A); profiles select only from it (TASK-110). Delete policy: RETAIN.</summary>
[SuppressMessage("Naming", "CA1711", Justification = "ERD D-4: the entity is the PascalCase of its table, identity_access.permission.")]
public sealed class Permission : AuditedEntity
{
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    public required string PermissionGroup { get; set; }

    /// <summary>The highest data classification the permission may expose (ADR-010; taxonomy outstanding).</summary>
    public Guid? DataClassificationItemId { get; set; }

    /// <summary>Triggers step-up authentication (TASK-029).</summary>
    public bool IsPrivileged { get; set; }
}
