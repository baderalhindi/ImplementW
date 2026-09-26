namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// One active assignment: the canonical role (R01–R08) at the root of the permission profile version the user is
/// bound to (ADR-018), with its scope anchors. An entity Project Manager is an EXTERNAL user holding R04 with
/// <see cref="ProjectId"/> set (ADR-013).
/// </summary>
public sealed record SessionRoleAssignment(
    string RoleCode,
    Guid PermissionProfileVersionId,
    Guid? DepartmentId,
    Guid? ExternalEntityId,
    Guid? ProjectId);
