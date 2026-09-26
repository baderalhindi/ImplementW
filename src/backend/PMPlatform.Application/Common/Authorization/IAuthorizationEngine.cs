namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// The backend-authoritative authorization engine (Blueprint Section 10.1, TASK-030, CTL-08). Every protected endpoint
/// passes through it; hiding a control in the SPA is presentation, never the protection (solution-architecture T-4).
/// </summary>
public interface IAuthorizationEngine
{
    /// <summary>The user and their grants in force now, read once per request.</summary>
    public Task<AuthorizationPrincipal?> GetPrincipalAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Effective authorization = permission + data scope + project/business relationship + assignment/ownership +
    /// record/lifecycle state + workflow authority + sensitivity restriction.
    /// </summary>
    public Task<AuthorizationDecision> AuthorizeAsync(Guid userId, AuthorizationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The fields of <paramref name="entityCode"/> the user may not see when reading it under
    /// <paramref name="permissionCode"/>: those classified above that permission's clearance (ADR-010).
    /// </summary>
    public Task<FieldMask> GetFieldMaskAsync(Guid userId, string permissionCode, string entityCode, CancellationToken cancellationToken);
}
