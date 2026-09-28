using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>Platform reference data with no record scope, read without a caller (TASK-034).</summary>
internal sealed class RoleDirectory(IRoleAdministrationRepository roles) : IRoleDirectory
{
    public Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken) => roles.ListRolesAsync(cancellationToken);
}
