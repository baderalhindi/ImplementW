using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// A WebAuthn passkey a person registered on one of their devices (13 Nov 2026 review): Face ID, Touch ID or the device's
/// own unlock. The private key never leaves the device; the platform keeps the public key and checks each signature with it.
/// </summary>
public sealed class UserPasskey : AuditedEntity
{
    public Guid UserId { get; set; }

    /// <summary>The authenticator's credential id, unique across all users.</summary>
    public required byte[] CredentialId { get; set; }

    /// <summary>The COSE-encoded public key.</summary>
    public required byte[] PublicKey { get; set; }

    /// <summary>The authenticator's signature counter at the last use; a counter that goes backwards suggests a cloned key.</summary>
    public long SignatureCounter { get; set; }

    /// <summary>What the person calls the device, such as "iPhone".</summary>
    public required string Name { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}
