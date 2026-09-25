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
}
