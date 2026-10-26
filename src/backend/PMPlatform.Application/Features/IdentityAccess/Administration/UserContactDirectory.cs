using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>ADR-004: only a verified number of an active user is ever handed out.</summary>
internal sealed class UserContactDirectory(IUserAdministrationRepository users) : IUserContactDirectory
{
    public async Task<string?> FindVerifiedMobileNumberAsync(Guid userId, CancellationToken cancellationToken) =>
        (await users.FindDetailAsync(userId, cancellationToken).ConfigureAwait(false))?.Value is { Status: UserStatus.Active, MobileVerifiedAt: not null, MobileNumber: { } mobileNumber }
            ? mobileNumber
            : null;
}
