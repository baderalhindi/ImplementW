using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// A named composition of catalogue permissions rooted at a canonical role; the R01–R08 defaults are profiles with
/// <see cref="IsShippedDefault"/> (ADR-018, TASK-110). Delete policy: RETAIN.
/// </summary>
public sealed class PermissionProfile : AuditedEntity
{
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    public Guid BaseRoleId { get; set; }

    public bool IsShippedDefault { get; set; }
}
