using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// A platform user as sign-in needs them, with the assignments active now. <see cref="ExternalEntityStatus"/> is the
/// status of the user's external entity, null for internal users. <see cref="MultiFactorEnrolled"/> is whether
/// <c>User.MfaEnrolledAt</c> is set (TASK-029).
/// </summary>
public sealed record UserAccess(
    Guid UserId,
    UserType UserType,
    UserStatus Status,
    ExternalEntityStatus? ExternalEntityStatus,
    string Username,
    string DisplayName,
    Language PreferredLanguage,
    bool MultiFactorEnrolled,
    IReadOnlyList<SessionRoleAssignment> RoleAssignments)
{
    /// <summary>
    /// A person may hold a session only while their account is active and, if external, while their entity is (ADR-013).
    /// A SERVICE principal never signs in.
    /// </summary>
    public bool MaySignIn =>
        Status == UserStatus.Active
        && UserType != UserType.Service
        && (UserType != UserType.External || ExternalEntityStatus == Domain.IdentityAccess.ExternalEntityStatus.Active);
}
