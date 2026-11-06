using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// A role: the canonical R01 to R08, shipped and undeletable, or one an administrator created (the 5 Nov 2026 review, R4-6). A created role with
/// no assignment may be deleted; one with assignments is only deactivated. Its allowed scopes and landing dashboard are its own data, never read from its code.
/// </summary>
public sealed class Role : AuditedEntity
{
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    public bool IsSystem { get; set; } = true;

    /// <summary>True for R04 and R08 only (ADR-013).</summary>
    public bool IsExternalEligible { get; set; }

    /// <summary>False once retired: offered for no new assignment; assignments already made run until ended.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>What an assignment may anchor on: ALL (no anchor needed), DEPARTMENT, PROJECT, ENTITY.</summary>
    public string[] AllowedScopes { get; set; } = [RoleScopes.All];

    /// <summary>The dashboard its holders land on (PORTFOLIO, PROJECT, GOVERNANCE); null lands on none.</summary>
    public string? LandingDashboardCode { get; set; }
}
