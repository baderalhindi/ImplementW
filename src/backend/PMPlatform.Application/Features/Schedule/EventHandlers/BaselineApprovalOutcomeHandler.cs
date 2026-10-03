using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts.Events;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule.EventHandlers;

/// <summary>
/// Applies the outcome of a baseline's WF-11 run (ADR-003 §8.2 edges 21, 28). APPROVED activates the candidate and supersedes
/// the project's ACTIVE baseline in the same transaction (<see cref="BaselineActivation"/>); RETURNED returns it for its next
/// revision; REJECTED and WITHDRAWN end it. It runs inside the outbox dispatch transaction, under the project's schedule lock,
/// so the change commits exactly when the delivery mark does, and two outcomes for one project are applied one after the other.
/// </summary>
/// <remarks>
/// Idempotent on its own as well (EV-4, EV-5): an outcome applies only to the revision under review while it is SUBMITTED,
/// and applying it leaves that state, so the same outcome again, or one for an older revision, is audited as ignored. An
/// approved candidate that is no rebaseline when submitted can meet an APPROVED baseline that activated in the meantime; it
/// carries no change authorisation, so it is returned rather than activated (BR-SCH-034) and the ACTIVE baseline stays.
/// </remarks>
internal sealed class BaselineApprovalOutcomeHandler(
    IScheduleRepository repository, IProjectFactsReader projects, BaselineActivation activation, IAuditTrail audit, TimeProvider timeProvider) : IApprovalOutcomeHandler
{
    public string SubjectModule => ScheduleApprovalRouting.SubjectModule;

    public async Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        ProjectBaseline baseline = await repository.FindBaselineAsync(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false)
                                   ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no baseline.");
        ProjectFacts project = await projects.FindAsync(baseline.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Baseline {baseline.Id} names no project.");
        ProjectSchedule schedule = await repository.LockScheduleAsync(project.Id, cancellationToken).ConfigureAwait(false)
                                   ?? throw new InvalidOperationException($"Baseline {baseline.Id} has no schedule.");

        ScheduleSaveOutcome saved;
        if (baseline.Status != ProjectBaselineStatus.Submitted || baseline.RevisionNo != outcome.Subject.RevisionNo)
        {
            audit.Stage(ScheduleAudit.OutcomeIgnored(project, baseline, outcome));
            saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (outcome.Data.Decision == ApprovalOutcomeDecision.Approved)
        {
            saved = await ApproveAsync(project, schedule, baseline, outcome, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            (ProjectBaselineStatus status, string eventType) = EndOf(outcome.Data.Decision);
            End(project, baseline, outcome, status, eventType, reason: null);
            saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        // Throwing rolls the dispatch back, and the outcome is delivered again later (TASK-035 D-8).
        if (saved != ScheduleSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} could not be applied: {saved}.");
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ScheduleSaveOutcome> ApproveAsync(
        ProjectFacts project, ProjectSchedule schedule, ProjectBaseline baseline, ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ProjectBaseline? active = await repository.FindActiveBaselineAsync(project.Id, track: true, cancellationToken).ConfigureAwait(false);
        if (active is { BaselineType: BaselineType.Approved } && baseline.ChangeAuthorizationId is null)
        {
            End(project, baseline, outcome, ProjectBaselineStatus.Returned, ScheduleAuditEvents.BaselineReturned, ScheduleAudit.RebaselineNotAuthorized);
            return await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        return await activation.ActivateAsync(
            new Activation(
                outcome.Data.DecidedByUserId,
                project,
                baseline,
                active,
                await repository.ListActivitiesAsync(schedule.Id, track: false, cancellationToken).ConfigureAwait(false),
                await repository.ListDependenciesAsync(schedule.Id, cancellationToken).ConfigureAwait(false),
                ApprovalRequired: true,
                timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Where a run that ended without approval leaves the candidate.</summary>
    private static (ProjectBaselineStatus Status, string EventType) EndOf(ApprovalOutcomeDecision decision) => decision switch
    {
        ApprovalOutcomeDecision.Returned => (ProjectBaselineStatus.Returned, ScheduleAuditEvents.BaselineReturned),
        ApprovalOutcomeDecision.Rejected => (ProjectBaselineStatus.Rejected, ScheduleAuditEvents.BaselineRejected),
        ApprovalOutcomeDecision.Withdrawn => (ProjectBaselineStatus.Withdrawn, ScheduleAuditEvents.BaselineWithdrawn),
        ApprovalOutcomeDecision.Approved => throw new ArgumentOutOfRangeException(nameof(decision), decision, "An approval activates."),
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };

    /// <summary>The run ended without activation; the decision's author made the change.</summary>
    private void End(ProjectFacts project, ProjectBaseline baseline, ApprovalOutcomeRecorded outcome, ProjectBaselineStatus status, string eventType, string? reason)
    {
        baseline.Status = status;
        baseline.UpdatedAt = timeProvider.GetUtcNow();
        baseline.UpdatedBy = outcome.Data.DecidedByUserId;
        audit.Stage(ScheduleAudit.OutcomeApplied(eventType, project, baseline, outcome, reason));
    }
}
