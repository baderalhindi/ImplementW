using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval;

/// <summary>
/// Escalation mechanics (Blueprint Section 11): an overdue task of the current stage is replaced by a task for
/// WORKFLOW_POLICY's APPROVAL_ESCALATION_ROLE in the same stage, due afresh. The escalated task keeps its place in the
/// history, linked to its replacement. Escalation moves where authority is looked for; the replacement's decider is
/// held to the same authority rules as any other.
/// </summary>
internal sealed class ApprovalEscalation(IApprovalRepository repository, ApprovalPolicy policy, IRoleDirectory roles, IAuditTrail audit)
{
    /// <summary>
    /// The replacement task, staged; null when the escalation role already has a task in the stage (it is the task's own
    /// role, or the stage was escalated to it before), so there is nowhere further to go.
    /// </summary>
    /// <exception cref="ConfigurationMissingException">WORKFLOW_POLICY lacks the escalation role or the due days, or names no role.</exception>
    public async Task<ApprovalTask?> EscalateAsync(
        ApprovalInstance instance, IReadOnlyList<ApprovalTask> tasks, ApprovalTask task, NarrativeText? reason,
        Guid actorId, AuditActorType actorType, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(task);

        string roleCode = await policy.EscalationRoleCodeAsync(now, cancellationToken).ConfigureAwait(false);
        RoleSummary role = (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(r => r.Code == roleCode)
                           ?? throw new ConfigurationMissingException(
                               ConfigurationFamilyCodes.WorkflowPolicy, ConfigurationMissingReason.EntryInvalid, $"value {ApprovalPolicy.EscalationRole}");
        if (tasks.Any(t => t.SequenceNo == task.SequenceNo && t.AssignedRoleId == role.Id))
        {
            return null;
        }

        ApprovalTask replacement = ApprovalRows.NewTask(
            instance.Id, task.SequenceNo, role.Id, await policy.DueAtAsync(now, cancellationToken).ConfigureAwait(false), actorId, now);
        task.Status = ApprovalTaskStatus.Escalated;
        task.EscalatedToTaskId = replacement.Id;
        task.DecisionReason = reason;
        ApprovalRows.Touch(task, actorId, now);

        repository.Add(replacement);
        audit.Stage(ApprovalAudit.Escalated(actorId, actorType, instance, task));
        return replacement;
    }
}
