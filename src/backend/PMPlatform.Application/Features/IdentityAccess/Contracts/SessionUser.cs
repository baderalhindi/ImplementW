using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>The signed-in user and the platform role assignments the session carries.</summary>
public sealed record SessionUser(
    Guid Id,
    UserType UserType,
    string Username,
    string DisplayName,
    Language PreferredLanguage,
    AuthenticationMethod AuthenticationMethod,
    IReadOnlyList<SessionRoleAssignment> RoleAssignments);
