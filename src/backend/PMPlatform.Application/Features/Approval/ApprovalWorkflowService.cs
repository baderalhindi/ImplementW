using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval;

/// <summary>
/// The approval runtime for the people in it: inbox, history, decisions, escalation and withdrawal. Every decision is
/// re-authorised when it is made (<see cref="ApprovalAuthority"/>), and a run's change, its audit events and its
/// outcome are saved together. A second decision on a run that another decision changed first is refused, not merged.
/// </summary>
internal sealed class ApprovalWorkflowService(
    IApprovalRepository repository,
    ApprovalAuthority authority,
    ApprovalEscalation escalation,
    ApprovalOutcomes outcomes,
    ApprovalPolicy policy,
    IAuditTrail audit,
    TimeProvider timeProvider) : IApprovalWorkflowService
{
    public async Task<ApprovalInboxPage> ListInboxAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        List<ApprovalInboxItem> items = [];
        if (await authority.IsActiveInternalAsync(callerId, cancellationToken).ConfigureAwait(false))
        {
            IReadOnlyCollection<Guid> roleIds = await authority.CandidateRoleIdsAsync(callerId, cancellationToken).ConfigureAwait(false);
            foreach ((ApprovalTask task, ApprovalInstance instance) in await repository.FindCurrentTasksAsync(roleIds, cancellationToken).ConfigureAwait(false))
            {
                if ((await authority.ResolveAsync(callerId, instance, task, cancellationToken).ConfigureAwait(false)).Authority is { } acting)
                {
                    items.Add(new ApprovalInboxItem(
                        task.Id, task.SequenceNo, task.AssignedRoleId, task.DueAt, acting.DelegationId is null ? null : acting.HolderUserId, ApprovalMapping.ToSummary(instance)));
                }
            }
        }

        List<ApprovalInboxItem> ordered = [.. items.OrderBy(i => i.DueAt ?? DateTimeOffset.MaxValue).ThenBy(i => i.TaskId)];
        return new ApprovalInboxPage([.. ordered.Skip(page.Skip).Take(page.PageSize)], page.Page, page.PageSize, ordered.Count);
    }

    public async Task<AdministrationResult<ApprovalInstancePage>> ListInstancesAsync(Guid callerId, ApprovalInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.RequestedByCaller)
        {
            (IReadOnlyList<ApprovalInstance> own, int total) = await repository.ListRequestedByAsync(
                callerId, query.Statuses, query.Page.Skip, query.Page.PageSize, cancellationToken).ConfigureAwait(false);
            return new ApprovalInstancePage([.. own.Select(ApprovalMapping.ToSummary)], query.Page.Page, query.Page.PageSize, total);
        }

        if (query is not { SubjectModule: { } module, SubjectType: { } type, SubjectId: { } subjectId })
        {
            throw new ArgumentException("A query names the caller's own requests or one subject.", nameof(query));
        }

        List<ApprovalInstanceSummary> visible = [];
        foreach (ApprovalRun run in await repository.FindBySubjectAsync(module, type, subjectId, cancellationToken).ConfigureAwait(false))
        {
            if ((query.Statuses.Count == 0 || query.Statuses.Contains(run.Instance.Status))
                && await authority.CanViewAsync(callerId, run.Instance, run.Tasks, cancellationToken).ConfigureAwait(false))
            {
                visible.Add(ApprovalMapping.ToSummary(run.Instance));
            }
        }

        List<ApprovalInstanceSummary> ordered = [.. visible.OrderByDescending(i => i.RequestedAt).ThenByDescending(i => i.Id)];
        return new ApprovalInstancePage([.. ordered.Skip(query.Page.Skip).Take(query.Page.PageSize)], query.Page.Page, query.Page.PageSize, ordered.Count);
    }

    public async Task<AdministrationResult<ApprovalInstanceDetail>> GetInstanceAsync(Guid callerId, Guid instanceId, CancellationToken cancellationToken)
    {
        ApprovalInstance? instance = await repository.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (instance is null)
        {
            return AdministrationError.NotFound;
        }

        IReadOnlyList<ApprovalTask> tasks = await repository.GetTasksAsync(instance.Id, cancellationToken).ConfigureAwait(false);
        return await authority.CanViewAsync(callerId, instance, tasks, cancellationToken).ConfigureAwait(false)
            ? ApprovalMapping.ToDetail(instance, tasks)
            : AdministrationError.NotFound;
    }

    public async Task<AdministrationResult<ApprovalInstanceDetail>> DecideAsync(
        Guid callerId, Guid taskId, ApprovalTaskDecision decision, NarrativeText? reason, CancellationToken cancellationToken)
    {
        if (await LoadAsync(taskId, cancellationToken).ConfigureAwait(false) is not { } loaded)
        {
            return AdministrationError.NotFound;
        }

        (ApprovalInstance instance, IReadOnlyList<ApprovalTask> tasks, ApprovalTask task) = loaded;

        bool actionable = ApprovalStages.IsActionable(instance, tasks, task);
        AuthorityCheck check = actionable
            ? await authority.ResolveAsync(callerId, instance, task, cancellationToken).ConfigureAwait(false)
            : AuthorityCheck.Refused(ApprovalRefusal.NoAuthority);
        if (check.Authority is not { } acting)
        {
            return await RefuseAsync(callerId, AuditValue.Format(decision)!, instance, tasks, task, actionable, check.Refusal, cancellationToken).ConfigureAwait(false);
        }

        if (decision != ApprovalTaskDecision.Approve && reason is null)
        {
            return AdministrationError.Rule(ApprovalErrorCodes.ReasonRequired, new FieldIssue("reason", FieldIssue.Required));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        task.Status = decision switch
        {
            ApprovalTaskDecision.Approve => ApprovalTaskStatus.Approved,
            ApprovalTaskDecision.Reject => ApprovalTaskStatus.Rejected,
            ApprovalTaskDecision.Return => ApprovalTaskStatus.Returned,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
        };
        task.AssignedUserId = acting.HolderUserId;
        task.ActingUserId = callerId;
        task.ApprovalDelegationId = acting.DelegationId;
        task.EligibilityRevalidatedAt = now;
        task.DecidedAt = now;
        task.DecisionReason = reason;
        ApprovalRows.Touch(task, callerId, now);
        audit.Stage(ApprovalAudit.Decided(
            decision switch
            {
                ApprovalTaskDecision.Approve => ApprovalAuditEvents.TaskApproved,
                ApprovalTaskDecision.Reject => ApprovalAuditEvents.TaskRejected,
                ApprovalTaskDecision.Return => ApprovalAuditEvents.TaskReturned,
                _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
            },
            callerId,
            instance,
            task));

        if (decision == ApprovalTaskDecision.Approve)
        {
            await AdvanceAsync(instance, tasks, task.SequenceNo, callerId, now, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Cancel(tasks, callerId, now);
            outcomes.Complete(
                instance,
                decision == ApprovalTaskDecision.Reject ? ApprovalOutcomeDecision.Rejected : ApprovalOutcomeDecision.Returned,
                callerId,
                AuditActorType.User,
                now);
        }

        return await SaveAsync(instance, tasks, callerId, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<ApprovalInstanceDetail>> EscalateAsync(Guid callerId, Guid taskId, NarrativeText? reason, CancellationToken cancellationToken)
    {
        if (await LoadAsync(taskId, cancellationToken).ConfigureAwait(false) is not { } loaded)
        {
            return AdministrationError.NotFound;
        }

        (ApprovalInstance instance, IReadOnlyList<ApprovalTask> tasks, ApprovalTask task) = loaded;

        if (instance.RequestedByUserId != callerId)
        {
            return await RefuseAsync(callerId, "ESCALATE", instance, tasks, task, actionable: true, ApprovalRefusal.NotRequester, cancellationToken).ConfigureAwait(false);
        }

        if (instance.Status != ApprovalInstanceStatus.Pending || task.Status != ApprovalTaskStatus.Pending)
        {
            return AdministrationError.TerminalState;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        bool overdue = ApprovalStages.IsActionable(instance, tasks, task) && task.DueAt <= now && !ApprovalStages.IsEscalationTarget(tasks, task);
        ApprovalTask? replacement = overdue
            ? await escalation.EscalateAsync(instance, tasks, task, reason, callerId, AuditActorType.User, now, cancellationToken).ConfigureAwait(false)
            : null;
        return replacement is null
            ? AdministrationError.Rule(ApprovalErrorCodes.EscalationNotAllowed, new FieldIssue("taskId", FieldIssue.NotAllowed))
            : await SaveAsync(instance, [.. tasks, replacement], callerId, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<ApprovalInstanceDetail>> WithdrawAsync(Guid callerId, Guid instanceId, CancellationToken cancellationToken)
    {
        ApprovalInstance? instance = await repository.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (instance is null)
        {
            return AdministrationError.NotFound;
        }

        IReadOnlyList<ApprovalTask> tasks = await repository.GetTasksAsync(instance.Id, cancellationToken).ConfigureAwait(false);
        if (instance.RequestedByUserId != callerId)
        {
            return await RefuseAsync(callerId, "WITHDRAW", instance, tasks, task: null, actionable: true, ApprovalRefusal.NotRequester, cancellationToken).ConfigureAwait(false);
        }

        if (instance.Status != ApprovalInstanceStatus.Pending)
        {
            return AdministrationError.TerminalState;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Cancel(tasks, callerId, now);
        outcomes.Complete(instance, ApprovalOutcomeDecision.Withdrawn, callerId, AuditActorType.User, now);
        return await SaveAsync(instance, tasks, callerId, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>After an approval: the run ends APPROVED when no task is left, or the next stage starts its clock when this one is done.</summary>
    private async Task AdvanceAsync(
        ApprovalInstance instance, IReadOnlyList<ApprovalTask> tasks, short decidedStage, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        short? current = ApprovalStages.Current(tasks);
        if (current is null)
        {
            outcomes.Complete(instance, ApprovalOutcomeDecision.Approved, actorId, AuditActorType.User, now);
        }
        else if (current != decidedStage)
        {
            DateTimeOffset dueAt = await policy.DueAtAsync(now, cancellationToken).ConfigureAwait(false);
            foreach (ApprovalTask next in tasks.Where(t => t.SequenceNo == current && t.Status == ApprovalTaskStatus.Pending))
            {
                next.DueAt = dueAt;
                ApprovalRows.Touch(next, actorId, now);
            }
        }
    }

    private static void Cancel(IReadOnlyList<ApprovalTask> tasks, Guid actorId, DateTimeOffset now)
    {
        foreach (ApprovalTask open in tasks.Where(t => t.Status == ApprovalTaskStatus.Pending))
        {
            open.Status = ApprovalTaskStatus.Cancelled;
            ApprovalRows.Touch(open, actorId, now);
        }
    }

    /// <summary>
    /// Why an attempt is refused, as R-47 answers it: a run the caller may not see is 404; a task no longer open is 409;
    /// otherwise 403. Every refusal of a caller without authority is audited (CTL-25).
    /// </summary>
    private async Task<AdministrationError> RefuseAsync(
        Guid callerId, string attempted, ApprovalInstance instance, IReadOnlyList<ApprovalTask> tasks, ApprovalTask? task, bool actionable,
        ApprovalRefusal refusal, CancellationToken cancellationToken)
    {
        if (!await authority.CanViewAsync(callerId, instance, tasks, cancellationToken).ConfigureAwait(false))
        {
            await audit.RecordAsync(ApprovalAudit.Refused(callerId, attempted, instance, task, refusal)).ConfigureAwait(false);
            return AdministrationError.NotFound;
        }

        if (!actionable)
        {
            return instance.Status != ApprovalInstanceStatus.Pending || task?.Status != ApprovalTaskStatus.Pending
                ? AdministrationError.TerminalState
                : AdministrationError.InvalidTransition;
        }

        await audit.RecordAsync(ApprovalAudit.Refused(callerId, attempted, instance, task, refusal)).ConfigureAwait(false);
        return AdministrationError.Forbidden;
    }

    private async Task<(ApprovalInstance Instance, IReadOnlyList<ApprovalTask> Tasks, ApprovalTask Task)?> LoadAsync(Guid taskId, CancellationToken cancellationToken)
    {
        ApprovalTask? task = await repository.FindTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        ApprovalInstance? instance = task is null ? null : await repository.FindInstanceAsync(task.ApprovalInstanceId, cancellationToken).ConfigureAwait(false);
        return instance is null ? null : (instance, await repository.GetTasksAsync(instance.Id, cancellationToken).ConfigureAwait(false), task!);
    }

    /// <summary>
    /// Every change to a run also changes the run's row, so two decisions on one run cannot both be saved: the second
    /// meets a changed row version and is refused as a conflict.
    /// </summary>
    private async Task<AdministrationResult<ApprovalInstanceDetail>> SaveAsync(
        ApprovalInstance instance, IReadOnlyList<ApprovalTask> tasks, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ApprovalRows.Touch(instance, actorId, now);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            ApprovalSaveOutcome.Saved => ApprovalMapping.ToDetail(instance, tasks),
            ApprovalSaveOutcome.ConcurrencyConflict => AdministrationError.Conflict(ApprovalErrorCodes.ConcurrentDecision),
            ApprovalSaveOutcome.DuplicateRun => throw new InvalidOperationException("A decision created a second run of a subject revision."),
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }
}
