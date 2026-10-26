using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>ADM-007 Role Detail, with the permission profiles rooted at the role (ADR-018).</summary>
public sealed record RoleDetail(
    Guid Id,
    string Code,
    BilingualLabel Name,
    bool IsSystem,
    bool IsExternalEligible,
    IReadOnlyList<PermissionProfileSummary> Profiles,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
