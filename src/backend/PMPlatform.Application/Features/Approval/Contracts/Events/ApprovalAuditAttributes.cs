namespace PMPlatform.Application.Features.Approval.Contracts.Events;

/// <summary>The attribute names of Approval's audit events.</summary>
public static class ApprovalAuditAttributes
{
    public const string ApprovalInstanceId = "approval_instance_id";
    public const string SubjectModule = "subject_module";
    public const string SubjectType = "subject_type";
    public const string SubjectId = "subject_id";
    public const string SubjectRevisionNo = "subject_revision_no";
    public const string RoutingKey = "routing_key";
    public const string AuthorityConfigurationVersionId = "authority_configuration_version_id";
    public const string SequenceNo = "sequence_no";
    public const string AssignedRoleId = "assigned_role_id";
    public const string AuthorityUserId = "authority_user_id";
    public const string ApprovalDelegationId = "approval_delegation_id";
    public const string EscalatedToTaskId = "escalated_to_task_id";
    public const string Status = "status";
    public const string Decision = "decision";
    public const string RefusalReason = "refusal_reason";
    public const string DelegateUserId = "delegate_user_id";
    public const string ValidFrom = "valid_from";
    public const string ValidTo = "valid_to";
}
