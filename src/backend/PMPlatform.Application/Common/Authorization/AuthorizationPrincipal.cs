using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// A user as the engine sees them on this request, read from the database rather than the token, so a disabled account,
/// an ended assignment or a changed profile takes effect on the next check without a new sign-in (TASK-031).
/// </summary>
public sealed record AuthorizationPrincipal(
    Guid UserId,
    UserType UserType,
    bool IsActive,
    Guid? DepartmentId,
    Guid? ExternalEntityId,
    IReadOnlyList<EffectiveGrant> Grants)
{
    public IEnumerable<string> RoleCodes => Grants.Select(g => g.RoleCode).Distinct(StringComparer.Ordinal);
}
