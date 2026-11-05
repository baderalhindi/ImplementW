using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

internal sealed class UserLookup(IUserAdministrationRepository users) : IUserLookup
{
    public Task<Guid?> FindIdByUsernameAsync(string username, CancellationToken cancellationToken) => users.FindIdByUsernameAsync(username, cancellationToken);

    public Task<IReadOnlyList<UserIdentity>> ListActiveAsync(CancellationToken cancellationToken) => users.ListActiveIdentitiesAsync(cancellationToken);

    public Task<IReadOnlyList<UserIdentity>> ListActiveOfEntityAsync(Guid externalEntityId, CancellationToken cancellationToken) =>
        users.ListActiveIdentitiesOfEntityAsync(externalEntityId, cancellationToken);
}
