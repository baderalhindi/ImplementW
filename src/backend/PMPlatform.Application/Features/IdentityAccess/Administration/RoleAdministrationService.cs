using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-006–009 (TASK-031). Roles, permissions and profiles are platform-wide reference data with no record scope, so the
/// endpoint gate (ROLE_VIEW, ROLE_MANAGE) is their whole authorization. The only write is a role's bilingual name, which
/// belongs to AHDA's administrators (seed: wording is inserted once and never reverted).
/// </summary>
internal sealed partial class RoleAdministrationService(
    IRoleAdministrationRepository roles,
    TimeProvider timeProvider,
    ILogger<RoleAdministrationService> logger) : IRoleAdministrationService
{
    public Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken) => roles.ListRolesAsync(cancellationToken);

    public async Task<AdministrationResult<Versioned<RoleDetail>>> GetRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
        await roles.FindRoleAsync(roleId, cancellationToken).ConfigureAwait(false) is { } role ? role : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<RoleDetail>>> RenameRoleAsync(
        Guid actorId, Guid roleId, BilingualLabel name, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        Role? role = await roles.FindRoleForUpdateAsync(roleId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return AdministrationError.NotFound;
        }

        role.Name = name;
        role.UpdatedAt = timeProvider.GetUtcNow();
        role.UpdatedBy = actorId;
        if ((await roles.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogRenamed(logger, actorId, role.Code);
        return await roles.FindRoleAsync(roleId, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Role {roleId} was saved and cannot be read back.");
    }

    public Task<IReadOnlyList<PermissionSummary>> ListPermissionsAsync(CancellationToken cancellationToken) => roles.ListPermissionsAsync(cancellationToken);

    public Task<PermissionProfilePage> ListProfilesAsync(Guid? baseRoleId, PageRequest page, CancellationToken cancellationToken) =>
        roles.ListProfilesAsync(baseRoleId, page, cancellationToken);

    public async Task<AdministrationResult<PermissionProfileDetail>> GetProfileAsync(Guid profileId, CancellationToken cancellationToken) =>
        await roles.FindProfileAsync(profileId, cancellationToken).ConfigureAwait(false) is { } profile ? profile : AdministrationError.NotFound;

    [LoggerMessage(Level = LogLevel.Information, Message = "Role administration: {ActorId} renamed role {RoleCode}.")]
    private static partial void LogRenamed(ILogger logger, Guid actorId, string roleCode);
}
