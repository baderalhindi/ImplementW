using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// A canonical role, R01 to R08: shipped, undeletable, and referenced by approval routing, landing dashboards and
/// notification matrices. Delete policy: RETAIN.
/// </summary>
public sealed class Role : AuditedEntity
{
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    public bool IsSystem { get; set; } = true;

    /// <summary>True for R04 and R08 only (ADR-013).</summary>
    public bool IsExternalEligible { get; set; }
}
