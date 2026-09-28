namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The identity and access refusals the API's request pipeline makes before any service runs (TASK-033, CTL-25): an
/// access token that fails validation, a privileged operation without a fresh second factor, and a permission gate
/// refused because the session never passed MFA. Refusals by the authorization engine are recorded by the engine.
/// </summary>
public interface IAccessAudit
{
    /// <summary>A presented access token was refused. <paramref name="requestPath"/> is the path only, never the query string.</summary>
    public Task AccessTokenRejectedAsync(AuthenticationFailureReason reason, string requestPath);

    /// <summary>A step-up operation (ADR-010) was refused for want of a fresh second factor.</summary>
    public Task StepUpRequiredAsync(Guid userId, string operation);

    /// <summary>A permission gate was refused because the user holds a role that requires MFA and the session never passed it.</summary>
    public Task MultiFactorGateDeniedAsync(Guid userId, string permissionCode);
}
