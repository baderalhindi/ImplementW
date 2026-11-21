using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The signed-in person's own passkeys (13 Nov 2026 review): Face ID, Touch ID or the device's unlock, registered on the
/// device in hand. Adding one needs a recent sign-in, so a session taken over later cannot plant a key of its own.
/// </summary>
public interface IPasskeyService
{
    public bool IsAvailable { get; }

    public Task<PasskeyList> ListAsync(Guid userId, CancellationToken cancellationToken);

    public Task<AdministrationResult<PasskeyCeremony>> BeginRegistrationAsync(Guid userId, SessionAuthentication session, CancellationToken cancellationToken);

    public Task<AdministrationResult<PasskeySummary>> RegisterAsync(
        Guid userId, SessionAuthentication session, string transaction, string credentialJson, string name, CancellationToken cancellationToken);

    public Task<AdministrationResult<PasskeyList>> RemoveAsync(Guid userId, Guid passkeyId, CancellationToken cancellationToken);
}
