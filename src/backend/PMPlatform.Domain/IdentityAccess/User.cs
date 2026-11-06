using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// A platform user (TASK-031). Department, manager and job title are directory-authoritative (ADR-007); session and
/// MFA factor state stay with the identity and MFA providers. Delete policy: RETAIN.
/// </summary>
public sealed class User : AuditedEntity
{
    public UserType UserType { get; set; }

    /// <summary>The directory or identity-provider subject; unique when present.</summary>
    public string? DirectorySubjectId { get; set; }

    public required string Username { get; set; }

    public required string DisplayName { get; set; }

    public required string Email { get; set; }

    /// <summary>E.164; verified before any SMS is sent (ADR-004).</summary>
    public string? MobileNumber { get; set; }

    public DateTimeOffset? MobileVerifiedAt { get; set; }

    public string? JobTitle { get; set; }

    /// <summary>Null for external users.</summary>
    public Guid? DepartmentId { get; set; }

    public Guid? ManagerUserId { get; set; }

    /// <summary>Required when <see cref="UserType"/> is <see cref="UserType.External"/>.</summary>
    public Guid? ExternalEntityId { get; set; }

    public Language PreferredLanguage { get; set; } = Language.Ar;

    public UserStatus Status { get; set; }

    public DateTimeOffset? DisabledAt { get; set; }

    /// <summary>MFA enrolment is recorded here; the factors are held by the MFA provider (TASK-029).</summary>
    public DateTimeOffset? MfaEnrolledAt { get; set; }

    /// <summary>Only the verification reference is stored (data minimisation, OQ-007).</summary>
    public string? NafathVerificationReference { get; set; }

    public DateTimeOffset? NafathVerifiedAt { get; set; }

    // The 5 Nov 2026 review (R4-6): a local account signs in with a password the platform holds, never the plain text.
    public AccountSource AccountSource { get; set; } = AccountSource.Directory;

    /// <summary>PBKDF2 hash; set on a local account only.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>The password is temporary: sign-in issues no session until it is changed.</summary>
    public bool MustChangePassword { get; set; }

    public int FailedSignInCount { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>When the password last changed, in whole seconds as a token's <c>auth_time</c>: a session authenticated before it is not continued.</summary>
    public DateTimeOffset? PasswordChangedAt { get; set; }

    /// <summary>When a reset link was last sent, so that repeated requests cannot flood the person's mailbox.</summary>
    public DateTimeOffset? PasswordResetRequestedAt { get; set; }

    /// <summary>The last day the account may be used, on any sign-in path; null when open-ended.</summary>
    public DateOnly? AccountEndsOn { get; set; }
}
