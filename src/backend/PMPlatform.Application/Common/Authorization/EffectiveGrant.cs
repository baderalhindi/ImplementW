using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// One permission a user holds now: a grant of the profile version an active assignment is bound to (ADR-018), with that
/// assignment's scope anchors. <see cref="ClearanceItemId"/> is the permission's DATA_CLASSIFICATION ceiling (ADR-010).
/// </summary>
public sealed record EffectiveGrant(
    string RoleCode,
    string PermissionCode,
    DataScope Scope,
    Guid? DepartmentId,
    Guid? ExternalEntityId,
    Guid? ProjectId,
    Guid? ClearanceItemId);
