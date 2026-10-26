namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>A role the session holds: the canonical role code, the profile version it is bound to (ADR-018) and its scope anchors.</summary>
public sealed record RoleAssignmentSummary(string RoleCode, Guid PermissionProfileVersionId, Guid? DepartmentId, Guid? ExternalEntityId, Guid? ProjectId);
