namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// Why no session was issued. <see cref="Rejected"/> and <see cref="IdentityNotVerified"/> concern the person; the others
/// concern the platform.
/// </summary>
public enum AuthenticationFailure
{
    /// <summary>
    /// Unknown user, wrong password, no platform account, disabled account, invalid or expired token: one outcome, so
    /// the response never tells a caller which of these it was (no user enumeration).
    /// </summary>
    Rejected = 1,

    /// <summary>The directory or identity provider could not be reached or answered with an error.</summary>
    ProviderUnavailable = 2,

    /// <summary>This environment has no configuration for the requested sign-in method.</summary>
    NotConfigured = 3,

    /// <summary>
    /// Nafath answered and did not verify the person (TASK-068): a code that is expired or already used, a transaction or
    /// state that is not this verification's, an ID token that fails validation. It concerns a person who has already
    /// signed in, so it is said as such; they may start the verification again.
    /// </summary>
    IdentityNotVerified = 4,
}
