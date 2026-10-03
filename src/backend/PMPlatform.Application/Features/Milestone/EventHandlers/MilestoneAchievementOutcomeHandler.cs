using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Milestone;

namespace PMPlatform.Application.Features.Milestone.EventHandlers;

/// <summary>
/// Applies the outcome of an achievement revision's WF-11 run (ADR-003 §8.2 edges 25, 28): acceptance stays with WF-05. An
/// approval makes the revision ACCEPTED, with its claimed date as the milestone's accepted Actual Achievement Date; the
/// milestone's previously accepted revision, if any, becomes SUPERSEDED and keeps everything else it held; and WF-03 records the
/// milestone ACHIEVED (edge 10). A return, rejection or withdrawal makes it RETURNED, with the approver's reason. It all runs
/// inside the outbox dispatch transaction, so it commits exactly when the delivery mark does.
/// </summary>
/// <remarks>
/// Idempotent on its own as well (EV-4, EV-5): an outcome applies only to the revision under review while it is SUBMITTED, and
/// applying it leaves that state, so the same outcome again, or one for another revision, is audited as ignored. An approval
/// for a milestone cancelled in the schedule meanwhile cannot be accepted, so the revision is returned instead.
/// </remarks>
internal sealed class MilestoneAchievementOutcomeHandler(
    IMilestoneRepository repository,
    IProjectFactsReader projects,
    IMilestoneAchievementRecorder schedule,
    IApprovalRunReader runs,
    IAuditTrail audit,
    TimeProvider timeProvider) : IApprovalOutcomeHandler
{
    public string SubjectModule => MilestoneApprovalRouting.SubjectModule;

    public async Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        await using IMilestoneWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        MilestoneAchievement achievement = await repository.FindAsync(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false)
                                           ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no milestone achievement.");
        ProjectFacts project = await projects.FindAsync(achievement.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Milestone achievement {achievement.Id} names no project.");

        MilestoneSaveOutcome saved;
        if (achievement.Status != MilestoneAchievementStatus.Submitted || achievement.RevisionNo != outcome.Subject.RevisionNo)
        {
            audit.Stage(MilestoneAudit.OutcomeIgnored(project, achievement, outcome));
            saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (MilestoneAchievementWorkflow.OutcomeOf(outcome.Data.Decision) == MilestoneAchievementStatus.Accepted)
        {
            saved = await AcceptAsync(project, achievement, outcome, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Return(project, achievement, outcome, await DecisionReasonAsync(outcome, cancellationToken).ConfigureAwait(false), auditReason: null);
            saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        // Throwing rolls the dispatch back, and the outcome is delivered again later (TASK-035 D-8).
        if (saved != MilestoneSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} could not be applied: {saved}.");
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves twice in order: the previously accepted revision leaves ACCEPTED first, because the partial unique index on ACCEPTED
    /// rows is checked row by row; then this revision enters it. The dispatch commits both or neither.
    /// </summary>
    private async Task<MilestoneSaveOutcome> AcceptAsync(ProjectFacts project, MilestoneAchievement achievement, ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        (Guid decidedBy, DateTimeOffset now) = (outcome.Data.DecidedByUserId, timeProvider.GetUtcNow());
        if (!await schedule.RecordAchievedAsync(achievement.ProjectMilestoneId, decidedBy, achievement.Id, cancellationToken).ConfigureAwait(false))
        {
            Return(project, achievement, outcome, reason: null, MilestoneAudit.MilestoneCancelled);
            return await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        MilestoneAchievement? prior = await repository.FindAcceptedAsync(achievement.ProjectMilestoneId, cancellationToken).ConfigureAwait(false);
        if (prior is not null)
        {
            prior.Status = MilestoneAchievementStatus.Superseded;
            prior.SupersededByAchievementId = achievement.Id;
            prior.UpdatedAt = now;
            prior.UpdatedBy = decidedBy;
            audit.Stage(MilestoneAudit.Superseded(decidedBy, project, prior));
            if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) is not MilestoneSaveOutcome.Saved and var superseding)
            {
                return superseding;
            }
        }

        achievement.Status = MilestoneAchievementStatus.Accepted;
        achievement.AcceptedActualAchievementDate = achievement.ClaimedAchievementDate;
        Reviewed(achievement, decidedBy, now);
        audit.Stage(MilestoneAudit.Accepted(project, achievement, outcome, prior?.Id));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The claim goes back to its claimant; <paramref name="auditReason"/> says why when WF-11 did not decide it so.</summary>
    private void Return(ProjectFacts project, MilestoneAchievement achievement, ApprovalOutcomeRecorded outcome, NarrativeText? reason, string? auditReason)
    {
        achievement.Status = MilestoneAchievementStatus.Returned;
        achievement.ReturnReason = reason;
        Reviewed(achievement, outcome.Data.DecidedByUserId, timeProvider.GetUtcNow());
        audit.Stage(MilestoneAudit.Returned(project, achievement, outcome, auditReason));
    }

    private static void Reviewed(MilestoneAchievement achievement, Guid by, DateTimeOffset at)
    {
        achievement.ReviewedByUserId = by;
        achievement.ReviewedAt = at;
        achievement.UpdatedAt = at;
        achievement.UpdatedBy = by;
    }

    /// <summary>The reason the returning or rejecting approver gave, as WF-11 recorded it on the deciding task; none for a withdrawal.</summary>
    private async Task<NarrativeText?> DecisionReasonAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApprovalInstanceDetail> subjectRuns = await runs.FindBySubjectAsync(
            outcome.Subject.Module, outcome.Subject.Type, outcome.Subject.Id, cancellationToken).ConfigureAwait(false);
        return subjectRuns.SingleOrDefault(r => r.Id == outcome.Data.ApprovalInstanceId)?.Tasks
            .Where(t => t.Status is ApprovalTaskStatus.Returned or ApprovalTaskStatus.Rejected)
            .MaxBy(t => t.DecidedAt)?.DecisionReason;
    }
}
