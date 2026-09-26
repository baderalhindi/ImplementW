using Microsoft.AspNetCore.Authorization;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// The endpoint's gate (TASK-030, CTL-08): an authenticated caller who holds the catalogue permission
/// <paramref name="permissionCode"/>. It admits the caller to the operation, not to a record: an endpoint acting on a
/// record also asks the engine with that record's <c>AuthorizationSubject</c> and answers 404 or 403 by R-47.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
internal sealed class RequirePermissionAttribute(string permissionCode) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string PermissionCode { get; } = permissionCode;

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(PermissionCode)];
}
