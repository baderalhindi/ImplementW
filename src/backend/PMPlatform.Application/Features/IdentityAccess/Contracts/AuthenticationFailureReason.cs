namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// Why a sign-in, refresh or second factor failed, as the audit event records it (TASK-033). The caller is never told:
/// every one of these that concerns the person answers the same <c>Rejected</c> (TASK-028, no user enumeration).
/// </summary>
public enum AuthenticationFailureReason
{
    /// <summary>The directory or identity provider refused the credentials, or none were given.</summary>
    CredentialsRejected = 1,

    /// <summary>The directory knows the person; the platform has no user for them.</summary>
    NoPlatformAccount = 2,

    /// <summary>The user is disabled, or their external entity is not active.</summary>
    AccountInactive = 3,

    /// <summary>The MFA, identity verification, refresh or access token is malformed, forged, expired or used after its session ended.</summary>
    TokenInvalid = 4,

    /// <summary>The access token is outside its lifetime: expired, or (with a clock out of step) not yet valid.</summary>
    TokenExpired = 5,

    /// <summary>The second factor was wrong, expired or already used.</summary>
    SecondFactorRejected = 6,

    /// <summary>The user requires MFA and the session never passed it.</summary>
    MultiFactorMissing = 7,

    /// <summary>The user requires MFA, has no factor, and enrolment at sign-in is not allowed.</summary>
    EnrolmentNotAllowed = 8,

    /// <summary>The directory, identity provider, MFA provider or identity verification provider could not be reached.</summary>
    ProviderUnavailable = 9,

    /// <summary>The sign-in method, MFA provider or identity verification provider is not configured in this environment.</summary>
    NotConfigured = 10,

    /// <summary>The user must have their identity verified by Nafath and the session never had it (TASK-068).</summary>
    IdentityVerificationMissing = 11,

    /// <summary>Nafath did not verify the person: see <see cref="AuthenticationFailure.IdentityNotVerified"/>.</summary>
    IdentityNotVerified = 12,

    /// <summary>
    /// A verification token for a user who needs no verification: internal, already verified, or the feature turned off
    /// since the token was issued. Nafath is not called (TASK-068).
    /// </summary>
    IdentityVerificationNotRequired = 13,
}
