using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

internal sealed class UserLookup(IUserAdministrationRepository users) : IUserLookup
{
    public Task<Guid?> FindIdByUsernameAsync(string username, CancellationToken cancellationToken) => users.FindIdByUsernameAsync(username, cancellationToken);
}
