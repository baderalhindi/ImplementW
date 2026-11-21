namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Events;

/// <summary>
/// The audit event types IdentityAccess produces (TASK-033; event-conventions EV-1, catalogue row 6). Each is recorded
/// in the class named beside it. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class IdentityAccessAuditEvents
{
    // AUTHENTICATION
    public const string SignInSucceeded = "IdentityAccess.SignInSucceeded";
    public const string SignInFailed = "IdentityAccess.SignInFailed";
    public const string FirstFactorPassed = "IdentityAccess.FirstFactorPassed";
    public const string MultiFactorEnrolled = "IdentityAccess.MultiFactorEnrolled";
    public const string SessionRefreshed = "IdentityAccess.SessionRefreshed";
    public const string SessionRefreshFailed = "IdentityAccess.SessionRefreshFailed";
    public const string StepUpSucceeded = "IdentityAccess.StepUpSucceeded";
    public const string StepUpFailed = "IdentityAccess.StepUpFailed";
    public const string AccessTokenRejected = "IdentityAccess.AccessTokenRejected";
    public const string IdentityVerificationRequired = "IdentityAccess.IdentityVerificationRequired";
    public const string IdentityVerified = "IdentityAccess.IdentityVerified";
    public const string IdentityVerificationFailed = "IdentityAccess.IdentityVerificationFailed";

    // AUTHORIZATION_DENIAL
    public const string AccessDenied = "IdentityAccess.AccessDenied";
    public const string StepUpRequired = "IdentityAccess.StepUpRequired";

    // PERMISSION_CHANGE
    public const string RoleAssigned = "IdentityAccess.RoleAssigned";
    public const string RoleAssignmentEnded = "IdentityAccess.RoleAssignmentEnded";

    // PRIVILEGED_ACTION
    public const string UserCreated = "IdentityAccess.UserCreated";
    public const string UserUpdated = "IdentityAccess.UserUpdated";
    public const string UserActivated = "IdentityAccess.UserActivated";
    public const string UserDisabled = "IdentityAccess.UserDisabled";
    public const string UserDeleted = "IdentityAccess.UserDeleted";
    public const string UserNationalIdChanged = "IdentityAccess.UserNationalIdChanged";
    public const string UserNationalIdRevealed = "IdentityAccess.UserNationalIdRevealed";
    public const string DepartmentDeleted = "IdentityAccess.DepartmentDeleted";
    public const string UserPermissionOverrideSet = "IdentityAccess.UserPermissionOverrideSet";
    public const string UserPermissionOverrideCleared = "IdentityAccess.UserPermissionOverrideCleared";

    /// <summary>AUTHENTICATION: a local account changed its password: a temporary one at sign-in, its own while signed in, or through a reset link.</summary>
    public const string PasswordChanged = "IdentityAccess.PasswordChanged";

    /// <summary>AUTHENTICATION: a reset link was queued for a local account. A request naming no such account, or within the cooldown, sends nothing and records nothing.</summary>
    public const string PasswordResetRequested = "IdentityAccess.PasswordResetRequested";

    /// <summary>AUTHENTICATION: a person added a passkey (Face ID, Touch ID or device unlock) to their own account.</summary>
    public const string PasskeyRegistered = "IdentityAccess.PasskeyRegistered";

    /// <summary>AUTHENTICATION: a person removed one of their own passkeys.</summary>
    public const string PasskeyRemoved = "IdentityAccess.PasskeyRemoved";

    /// <summary>PRIVILEGED_ACTION: an administrator issued a local account a new temporary password.</summary>
    public const string PasswordReset = "IdentityAccess.PasswordReset";

    /// <summary>PRIVILEGED_ACTION: an administrator unlocked a local account.</summary>
    public const string UserUnlocked = "IdentityAccess.UserUnlocked";

    /// <summary>PRIVILEGED_ACTION: an administrator set or cleared an account's end date.</summary>
    public const string UserEndDateSet = "IdentityAccess.UserEndDateSet";

    /// <summary>PRIVILEGED_ACTION: an administrator created a role.</summary>
    public const string RoleCreated = "IdentityAccess.RoleCreated";

    /// <summary>PRIVILEGED_ACTION: an administrator changed a role's allowed scopes or landing dashboard.</summary>
    public const string RoleSettingsChanged = "IdentityAccess.RoleSettingsChanged";

    /// <summary>PRIVILEGED_ACTION: an administrator deactivated or reactivated a role.</summary>
    public const string RoleActivationChanged = "IdentityAccess.RoleActivationChanged";

    /// <summary>PRIVILEGED_ACTION: an administrator deleted a created role that was never assigned.</summary>
    public const string RoleDeleted = "IdentityAccess.RoleDeleted";
    public const string RoleRenamed = "IdentityAccess.RoleRenamed";
    public const string PermissionProfileCreated = "IdentityAccess.PermissionProfileCreated";
    public const string PermissionProfileVersionDrafted = "IdentityAccess.PermissionProfileVersionDrafted";
    public const string PermissionProfileGrantsChanged = "IdentityAccess.PermissionProfileGrantsChanged";
    public const string PermissionProfileVersionValidated = "IdentityAccess.PermissionProfileVersionValidated";
    public const string PermissionProfileVersionPublished = "IdentityAccess.PermissionProfileVersionPublished";
    public const string PermissionProfileVersionRetired = "IdentityAccess.PermissionProfileVersionRetired";
    public const string PermissionProfileAssignmentsMigrated = "IdentityAccess.PermissionProfileAssignmentsMigrated";
    public const string DepartmentCreated = "IdentityAccess.DepartmentCreated";
    public const string DepartmentUpdated = "IdentityAccess.DepartmentUpdated";
    public const string DepartmentActivated = "IdentityAccess.DepartmentActivated";
    public const string DepartmentDeactivated = "IdentityAccess.DepartmentDeactivated";
    public const string ExternalEntityCreated = "IdentityAccess.ExternalEntityCreated";
    public const string ExternalEntityUpdated = "IdentityAccess.ExternalEntityUpdated";
    public const string ExternalEntitySuspended = "IdentityAccess.ExternalEntitySuspended";
    public const string ExternalEntityActivated = "IdentityAccess.ExternalEntityActivated";
    public const string ExternalEntityRetired = "IdentityAccess.ExternalEntityRetired";

    /// <summary>PRIVILEGED_ACTION: an external entity nothing referenced was erased.</summary>
    public const string ExternalEntityDeleted = "IdentityAccess.ExternalEntityDeleted";
    public const string IdentityIntegrationTested = "IdentityAccess.IdentityIntegrationTested";
}
