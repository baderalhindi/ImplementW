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
    public const string RoleRenamed = "IdentityAccess.RoleRenamed";
    public const string DepartmentCreated = "IdentityAccess.DepartmentCreated";
    public const string DepartmentUpdated = "IdentityAccess.DepartmentUpdated";
    public const string DepartmentActivated = "IdentityAccess.DepartmentActivated";
    public const string DepartmentDeactivated = "IdentityAccess.DepartmentDeactivated";
    public const string ExternalEntityCreated = "IdentityAccess.ExternalEntityCreated";
    public const string ExternalEntityUpdated = "IdentityAccess.ExternalEntityUpdated";
    public const string ExternalEntitySuspended = "IdentityAccess.ExternalEntitySuspended";
    public const string ExternalEntityActivated = "IdentityAccess.ExternalEntityActivated";
    public const string ExternalEntityRetired = "IdentityAccess.ExternalEntityRetired";
    public const string IdentityIntegrationTested = "IdentityAccess.IdentityIntegrationTested";
}
