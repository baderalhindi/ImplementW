using Microsoft.AspNetCore.Authorization;

namespace PMPlatform.Api.Authorization;

/// <summary>The caller must hold <see cref="PermissionCode"/> at some scope, as the authorization engine decides it (TASK-030).</summary>
internal sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
