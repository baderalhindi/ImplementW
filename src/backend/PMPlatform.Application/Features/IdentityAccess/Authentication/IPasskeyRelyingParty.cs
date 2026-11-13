using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// The platform as a WebAuthn relying party (13 Nov 2026 review). It builds the options for the browser and verifies the
/// device's answer: origin, relying party id, challenge, user verification (Face ID, Touch ID or the device unlock),
/// signature and counter. <c>Identity:Passkeys</c> names the relying party. Implemented in Infrastructure/Identity.
/// </summary>
public interface IPasskeyRelyingParty
{
    /// <summary>False until a relying party id and an origin are known (<c>Identity:Passkeys</c> or <c>APP_BASE_URL</c>).</summary>
    public bool IsConfigured { get; }

    /// <summary>Asks the device for a new discoverable passkey, verified by the person, never one of <paramref name="registeredCredentialIds"/>.</summary>
    public PasskeyCeremony BeginRegistration(PasskeyHolder holder, IReadOnlyList<byte[]> registeredCredentialIds);

    /// <summary>The new credential, or null if the device's answer does not verify for this user's ceremony. Whether its id is taken is the caller's to check.</summary>
    public Task<NewPasskeyCredential?> CompleteRegistrationAsync(Guid userId, string transaction, string credentialJson, CancellationToken cancellationToken);

    /// <summary>Asks the device for any of this platform's passkeys: the person picks theirs, so no username is typed.</summary>
    public PasskeyCeremony BeginAssertion();

    /// <summary>The credential id the device answered with, to look its passkey up by; null if the answer cannot be read.</summary>
    public byte[]? CredentialIdOf(string credentialJson);

    /// <summary>The passkey's signature counter now, or null if the answer does not verify against <paramref name="stored"/> for this ceremony.</summary>
    public Task<long?> VerifyAssertionAsync(string transaction, string credentialJson, StoredPasskey stored, CancellationToken cancellationToken);
}

/// <summary>Who a new passkey is for, as the device shows it.</summary>
public sealed record PasskeyHolder(Guid UserId, string Username, string DisplayName);

/// <summary>A verified new credential, ready to store.</summary>
public sealed record NewPasskeyCredential(byte[] CredentialId, byte[] PublicKey, long SignatureCounter);

/// <summary>A registered passkey as verification needs it.</summary>
public sealed record StoredPasskey(Guid Id, Guid UserId, byte[] CredentialId, byte[] PublicKey, long SignatureCounter);
