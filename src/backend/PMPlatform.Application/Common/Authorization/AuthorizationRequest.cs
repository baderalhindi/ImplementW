namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// A permission, and the record it is exercised on. Without a <see cref="Subject"/> the request asks only whether the
/// caller holds the permission at some scope: the gate on an endpoint, before the record is known.
/// </summary>
public sealed record AuthorizationRequest(string PermissionCode, AuthorizationSubject? Subject = null)
{
    /// <summary>
    /// Counts only the grants the user holds through this role: an approval stage is assigned to a role (APPROVAL_AUTHORITY),
    /// so holding the permission through another role is not authority over it (TASK-035). Null: any role.
    /// </summary>
    public string? RoleCode { get; init; }
}
