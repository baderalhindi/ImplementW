namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Events;

/// <summary>
/// The attribute names IdentityAccess's audit events carry. None of them ever holds a password, token, second-factor
/// code or secret (CTL-27): <see cref="Username"/> is the name typed at sign-in, the rest are ids, codes and reasons.
/// </summary>
public static class IdentityAccessAuditAttributes
{
    public const string AuthenticationMethod = "authentication_method";
    public const string Username = "username";
    public const string FailureReason = "failure_reason";
    public const string MultiFactor = "multi_factor";
    public const string PermissionCode = "permission_code";
    public const string DenialReason = "denial_reason";
    public const string DecisionOutcome = "decision_outcome";
    public const string Operation = "operation";
    public const string RequestPath = "request_path";
    public const string DepartmentId = "department_id";
    public const string OwnerUserId = "owner_user_id";
    public const string UserId = "user_id";
    public const string RoleCode = "role_code";
    public const string PermissionProfileVersionId = "permission_profile_version_id";
    public const string ExternalEntityId = "external_entity_id";
    public const string ProjectId = "project_id";
    public const string SponsorUserId = "sponsor_user_id";
    public const string StartsAt = "starts_at";
    public const string EndsAt = "ends_at";
    public const string EndReason = "end_reason";
    public const string Status = "status";

    /// <summary>TASK-068: the platform's reference of a Nafath verification, the one thing kept of it (OQ-007).</summary>
    public const string VerificationReference = "verification_reference";
}
