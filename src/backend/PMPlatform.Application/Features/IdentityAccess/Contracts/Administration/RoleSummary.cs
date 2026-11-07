using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A row of ADM-006 Roles.</summary>
public sealed record RoleSummary(
    Guid Id, string Code, BilingualLabel Name, bool IsSystem, bool IsExternalEligible, bool IsActive = true, IReadOnlyList<string>? AllowedScopes = null, string? LandingDashboardCode = null, bool ShowCreateInHeader = true);

/// <summary>A new role (the 5 Nov 2026 review §6.2): <see cref="SourceRoleId"/> copies that role's published profile; null starts empty.</summary>
public sealed record RoleCreateDraft(string Code, BilingualLabel Name, bool IsExternalEligible, IReadOnlyList<string> AllowedScopes, Guid? SourceRoleId, string? LandingDashboardCode);
