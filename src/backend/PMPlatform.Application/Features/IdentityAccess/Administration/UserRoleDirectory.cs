using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>The roles of the assignments in force, as sign-in reads them, so a disabled person or an ended assignment lands nowhere.</summary>
internal sealed class UserRoleDirectory(IUserAccessRepository users) : IUserRoleDirectory
{
    public async Task<UserRoles> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false) is { MaySignIn: true } access
            ? new UserRoles(access.UserType == UserType.External, [.. access.RoleAssignments.Select(a => a.RoleCode).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)])
            : UserRoles.None;
}
