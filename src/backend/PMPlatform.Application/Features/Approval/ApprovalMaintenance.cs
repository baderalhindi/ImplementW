using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval;

/// <summary>
/// Escalates every overdue task of a current stage, one run per save so one failure touches no other run, and marks
/// delegations whose period has ended EXPIRED (they conveyed nothing after it already). Missing escalation
/// configuration stops the pass: it fails for every task alike, and is logged each pass until it is published.
/// </summary>
internal sealed partial class ApprovalMaintenance(
    IApprovalRepository repository, ApprovalEscalation escalation, IAuditTrail audit, TimeProvider timeProvider, ILogger<ApprovalMaintenance> logger)
    : IApprovalMaintenance
{
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int changed = 0;
        try
        {
            foreach (Guid taskId in await repository.FindOverdueTaskIdsAsync(now, batchSize, cancellationToken).ConfigureAwait(false))
            {
                changed += await EscalateAsync(taskId, now, cancellationToken).ConfigureAwait(false) ? 1 : 0;
            }
        }
        catch (ConfigurationMissingException missing)
        {
            LogEscalationConfigurationMissing(logger, missing.ConfigurationCode, missing.Entry);
        }

        IReadOnlyList<ApprovalDelegation> lapsed = await repository.FindLapsedDelegationsAsync(now, batchSize, cancellationToken).ConfigureAwait(false);
        foreach (ApprovalDelegation delegation in lapsed)
        {
            delegation.Status = ApprovalDelegationStatus.Expired;
            ApprovalRows.Touch(delegation, ApprovalServicePrincipal.Id, now);
            audit.Stage(ApprovalAudit.Delegation(ApprovalAuditEvents.DelegationExpired, ApprovalServicePrincipal.Id, AuditActorType.Service, delegation));
        }

        if (lapsed.Count > 0 && await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ApprovalSaveOutcome.Saved)
        {
            changed += lapsed.Count;
        }

        return changed;
    }

    private async Task<bool> EscalateAsync(Guid taskId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ApprovalTask? task = await repository.FindTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        ApprovalInstance? instance = task is null ? null : await repository.FindInstanceAsync(task.ApprovalInstanceId, cancellationToken).ConfigureAwait(false);
        if (instance is null)
        {
            return false;
        }

        IReadOnlyList<ApprovalTask> tasks = await repository.GetTasksAsync(instance.Id, cancellationToken).ConfigureAwait(false);
        if (!ApprovalStages.IsActionable(instance, tasks, task!)
            || await escalation.EscalateAsync(instance, tasks, task!, null, ApprovalServicePrincipal.Id, AuditActorType.Service, now, cancellationToken).ConfigureAwait(false) is null)
        {
            LogNotEscalated(logger, taskId);
            return false;
        }

        ApprovalRows.Touch(instance, ApprovalServicePrincipal.Id, now);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ApprovalSaveOutcome.Saved;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Overdue approval tasks were not escalated: {ConfigurationCode} {Entry} is missing.")]
    private static partial void LogEscalationConfigurationMissing(ILogger logger, string configurationCode, string? entry);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Overdue approval task {TaskId} has nowhere to be escalated: the escalation role already holds its stage.")]
    private static partial void LogNotEscalated(ILogger logger, Guid taskId);
}
