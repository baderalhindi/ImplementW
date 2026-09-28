using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

namespace PMPlatform.Application.Features.Approval;

/// <summary>
/// The WORKFLOW_POLICY values the approval runtime reads, resolved as of the moment they are used (E-U2). Neither has a
/// default: without them a stage cannot be given a due date and an overdue task has nowhere to go (record F-7).
/// </summary>
internal sealed class ApprovalPolicy(IConfigurationResolver resolver)
{
    /// <summary>DURATION_DAYS: how long a stage's approvers have, from the moment the stage is reached.</summary>
    public const string TaskDueDays = "APPROVAL_TASK_DUE_DAYS";

    /// <summary>TEXT: the code of the role an overdue task is escalated to.</summary>
    public const string EscalationRole = "APPROVAL_ESCALATION_ROLE";

    public async Task<DateTimeOffset> DueAtAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        now.AddDays((await resolver.ResolveAsync(ConfigurationFamilyCodes.WorkflowPolicy, now, cancellationToken).ConfigureAwait(false)).RequireDurationDays(TaskDueDays));

    public async Task<string> EscalationRoleCodeAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        (await resolver.ResolveAsync(ConfigurationFamilyCodes.WorkflowPolicy, now, cancellationToken).ConfigureAwait(false)).RequireText(EscalationRole);
}
