using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

internal sealed class PermissionProfileDirectory(IRoleAdministrationRepository roles) : IPermissionProfileDirectory
{
    public Task<Guid?> FindPublishedVersionIdAsync(string profileCode, CancellationToken cancellationToken) =>
        roles.FindPublishedProfileVersionIdAsync(profileCode, cancellationToken);

    public Task<IReadOnlyList<PermissionProfileSummary>> ListAssignableAsync(CancellationToken cancellationToken) =>
        roles.ListAssignableProfilesAsync(cancellationToken);
}
