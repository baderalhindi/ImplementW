using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>ADM-009 Permission Matrix for one profile: every version, newest first, with its grants.</summary>
public sealed record PermissionProfileDetail(
    Guid Id,
    string Code,
    BilingualLabel Name,
    Guid BaseRoleId,
    string BaseRoleCode,
    bool IsShippedDefault,
    IReadOnlyList<PermissionProfileVersionDetail> Versions);
