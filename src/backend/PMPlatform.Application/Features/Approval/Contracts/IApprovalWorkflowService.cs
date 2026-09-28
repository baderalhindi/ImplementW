using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// WF-11 for the people in it (SCR-100, 101, 115; MOD-040–045). Every decision revalidates the decider's authority at
/// the moment it is made (TASK-035): a role, scope, account or delegation that ended since the task was listed no longer counts.
/// </summary>
public interface IApprovalWorkflowService
{
    /// <summary>SCR-100: the tasks the caller may decide now, by their own authority or a delegator's.</summary>
    public Task<ApprovalInboxPage> ListInboxAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<ApprovalInstancePage>> ListInstancesAsync(Guid callerId, ApprovalInstanceQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<ApprovalInstanceDetail>> GetInstanceAsync(Guid callerId, Guid instanceId, CancellationToken cancellationToken);

    /// <summary>MOD-040–042. The last approval of the last stage, a rejection and a return each end the run and publish its outcome.</summary>
    public Task<AdministrationResult<ApprovalInstanceDetail>> DecideAsync(
        Guid callerId, Guid taskId, ApprovalTaskDecision decision, Domain.Common.NarrativeText? reason, CancellationToken cancellationToken);

    /// <summary>MOD-045: the requester escalates an overdue task of the current stage to WORKFLOW_POLICY's APPROVAL_ESCALATION_ROLE.</summary>
    public Task<AdministrationResult<ApprovalInstanceDetail>> EscalateAsync(
        Guid callerId, Guid taskId, Domain.Common.NarrativeText? reason, CancellationToken cancellationToken);

    /// <summary>MOD-044: the requester withdraws a PENDING run; its outcome is WITHDRAWN.</summary>
    public Task<AdministrationResult<ApprovalInstanceDetail>> WithdrawAsync(Guid callerId, Guid instanceId, CancellationToken cancellationToken);
}
