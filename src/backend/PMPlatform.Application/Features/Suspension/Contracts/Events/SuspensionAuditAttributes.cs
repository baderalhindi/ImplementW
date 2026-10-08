namespace PMPlatform.Application.Features.Suspension.Contracts.Events;

/// <summary>The attribute names of Suspension's audit events.</summary>
public static class SuspensionAuditAttributes
{
    public const string RequestType = "request_type";
    public const string Status = "status";
    public const string RevisionNo = "revision_no";
    public const string Reason = "reason";
    public const string RequestedEffectiveDate = "requested_effective_date";
    public const string PlannedResumptionDate = "planned_resumption_date";
    public const string ApprovalInstanceId = "approval_instance_id";
    public const string ApprovalDecision = "approval_decision";
    public const string ActiveSuspensionId = "active_suspension_id";
    public const string EffectedAt = "effected_at";
    public const string EndReason = "end_reason";
    public const string ClosureCaseId = "closure_case_id";
    public const string Permission = "permission";
    public const string RefusalReason = "refusal_reason";
}
