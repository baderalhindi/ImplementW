using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>One permission with its data scope inside a profile version (Appendix A). Delete policy: CASCADE.</summary>
public sealed class PermissionProfileGrant : AuditedEntity
{
    public Guid PermissionProfileVersionId { get; set; }

    public Guid PermissionId { get; set; }

    public DataScope DataScope { get; set; }
}
