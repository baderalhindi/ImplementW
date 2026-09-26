using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The signed-in user and the platform role assignments the session carries. <see cref="AuthenticatedAt"/> is when the
/// person last authenticated interactively (sign-in, second factor or step-up); a refresh does not move it.
/// </summary>
public sealed record SessionUser(
    Guid Id,
    UserType UserType,
    string Username,
    string DisplayName,
    Language PreferredLanguage,
    AuthenticationMethod AuthenticationMethod,
    bool MultiFactorAuthenticated,
    DateTimeOffset AuthenticatedAt,
    IReadOnlyList<SessionRoleAssignment> RoleAssignments);
