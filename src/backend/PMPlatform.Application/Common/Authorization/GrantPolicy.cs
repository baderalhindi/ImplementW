using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// The safety rules no permission profile can override (shipped-permission-matrix.md D-2): they hold in code, so a profile authored on
/// ADM-008 (TASK-110), a re-seed or a test grant cannot break them. The engine drops a grant they forbid as if the profile did not hold it.
/// </summary>
/// <remarks>
/// R01 administers and never holds business data or approval authority (Appendix A.1: "System Administrator business-record access … does
/// not create business authority"). An external user — whatever role, R08 or ADR-013's entity Project Manager in R04 — and any holder of R08 never hold approval,
/// the SAR amounts, reports or integration monitoring (ADR-013: AHDA retains every approval; ADR-019: the Project Dashboard and no reports;
/// BR-INT-022). Nobody approving their own submission is WF-11's person check (ApprovalAuthority), not a grant.
/// </remarks>
public static class GrantPolicy
{
    /// <summary>R01's role code.</summary>
    public const string SystemAdministratorRole = "R01";

    /// <summary>R08's role code.</summary>
    public const string ExternalEntityUserRole = "R08";

    /// <summary>The permission groups that hold business data or business authority.</summary>
    public static IReadOnlySet<string> BusinessGroups { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "APPROVAL", "PROJECT", "PROGRESS", "SCHEDULE", "PROJECT_TASK", "MILESTONE", "RISK", "MANAGEMENT_CONCERN", "CHANGE_REQUEST",
        "SUSPENSION", "CLOSEOUT", "EXTERNAL_PARTICIPATION", "FINANCIAL", "KPI", "DOCUMENT_MANAGEMENT", "REPORTS", "ACTIVITY",
        "AUDIT_BUSINESS", "AUDIT_FINANCIAL", "AUDIT_EXTERNAL",
    };

    /// <summary>The permission groups an external user, or any holder of R08, never holds.</summary>
    public static IReadOnlySet<string> InternalOnlyGroups { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "APPROVAL", "REPORTS", "INTEGRATION_MONITORING",
    };

    /// <summary>The permissions an external user never holds, outside <see cref="InternalOnlyGroups"/>.</summary>
    public static IReadOnlySet<string> InternalOnlyPermissions { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        PermissionCatalogue.FinancialViewSensitive,
    };

    /// <summary>Whether a grant of <paramref name="permission"/> through <paramref name="roleCode"/> may take effect for a user of <paramref name="userType"/>.</summary>
    public static bool Permits(string roleCode, UserType userType, PermissionDefinition permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return !(string.Equals(roleCode, SystemAdministratorRole, StringComparison.Ordinal) && BusinessGroups.Contains(permission.Group))
               && !((userType == UserType.External || string.Equals(roleCode, ExternalEntityUserRole, StringComparison.Ordinal))
                    && (InternalOnlyGroups.Contains(permission.Group) || InternalOnlyPermissions.Contains(permission.Code)));
    }
}
