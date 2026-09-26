namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>Why no session was issued. Only <see cref="Rejected"/> concerns the person; the others concern the platform.</summary>
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
}
