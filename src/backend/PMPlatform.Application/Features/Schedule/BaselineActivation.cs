using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// Makes a candidate the project's ACTIVE baseline (TASK-046), for submission under a profile that needs no approval
/// (ADR-015) and for WF-11's APPROVED outcome alike. It runs inside the caller's unit of work, which holds the project's
/// schedule lock, and saves three times in order: the ACTIVE baseline becomes SUPERSEDED; the working schedule — its live
/// activities, their dependencies and its live milestones' forecast dates — is copied into the candidate; the candidate becomes
/// ACTIVE. The partial unique index on ACTIVE rows is checked row by row, so the
/// old one must leave ACTIVE before the new one enters it; and the database lets a baseline's copy be written only before it
/// activates and lets an APPROVED baseline activate only once it has one. The caller commits all three or none: there is no
/// moment, seen from outside the transaction, with two ACTIVE baselines or with none where there was one.
/// </summary>
internal sealed class BaselineActivation(IScheduleRepository repository, ScheduleHealthProjection health, IAuditTrail audit)
{
    public async Task<ScheduleSaveOutcome> ActivateAsync(Activation activation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activation);
        (Guid actorId, ProjectFacts project, ProjectBaseline candidate, ProjectBaseline? prior, DateTimeOffset now) =
            (activation.ActorId, activation.Project, activation.Candidate, activation.Prior, activation.Now);

        if (prior is not null)
        {
            prior.Status = ProjectBaselineStatus.Superseded;
            prior.SupersededAt = now;
            prior.SupersededByBaselineId = candidate.Id;
            prior.UpdatedAt = now;
            prior.UpdatedBy = actorId;
            audit.Stage(ScheduleAudit.BaselineSuperseded(actorId, project, prior));
            if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) is not ScheduleSaveOutcome.Saved and var superseding)
            {
                return superseding;
            }
        }

        // The working schedule is frozen while its candidate is with WF-11, so this copy is what was reviewed.
        List<ScheduleActivity> live = [.. activation.Activities.Where(a => a.Status != ScheduleActivityStatus.Cancelled)];
        HashSet<Guid> liveIds = [.. live.Select(a => a.Id)];
        foreach (ScheduleActivity activity in live)
        {
            repository.Add(new BaselineActivity
            {
                Id = Guid.CreateVersion7(now),
                ProjectBaselineId = candidate.Id,
                ScheduleActivityId = activity.Id,
                ParentActivityId = activity.ParentActivityId,
                ActivityKind = activity.ActivityKind,
                PlannedStartDate = activity.PlannedStartDate,
                PlannedFinishDate = activity.PlannedFinishDate,
                PlannedDurationDays = activity.PlannedDurationDays,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        foreach (ScheduleDependency dependency in activation.Dependencies.Where(d => liveIds.Contains(d.PredecessorActivityId) && liveIds.Contains(d.SuccessorActivityId)))
        {
            repository.Add(new BaselineDependency
            {
                Id = Guid.CreateVersion7(now),
                ProjectBaselineId = candidate.Id,
                PredecessorActivityId = dependency.PredecessorActivityId,
                SuccessorActivityId = dependency.SuccessorActivityId,
                DependencyType = dependency.DependencyType,
                LagDays = dependency.LagDays,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        // A milestone's planned date in the baseline is its forecast as the baseline activates (TASK-050): the date WF-11 reviewed.
        foreach (ProjectMilestone milestone in activation.Milestones.Where(m => m.Status != ProjectMilestoneStatus.Cancelled))
        {
            repository.Add(new BaselineMilestone
            {
                Id = Guid.CreateVersion7(now),
                ProjectBaselineId = candidate.Id,
                ProjectMilestoneId = milestone.Id,
                PlannedDate = milestone.ForecastDate,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) is not ScheduleSaveOutcome.Saved and var copying)
        {
            return copying;
        }

        ProjectBaselineStatus from = candidate.Status;
        candidate.Status = ProjectBaselineStatus.Active;
        candidate.ActivatedAt = now;
        candidate.BaselineFinishDate = ScheduleCalculation.PlannedFinish(live) ?? candidate.BaselineFinishDate;
        candidate.UpdatedAt = now;
        candidate.UpdatedBy = actorId;
        audit.Stage(ScheduleAudit.BaselineActivated(actorId, project, candidate, from, prior, activation.ApprovalRequired));
        await health.StageAsync(actorId, project, activation.Activities, candidate, now, cancellationToken).ConfigureAwait(false);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// One activation: who, of which project, the candidate, the ACTIVE baseline it supersedes (null for the first), the working
/// schedule as it stands — activities, dependencies and milestones — and whether WF-11 approved it or the profile needed no approval.
/// </summary>
internal sealed record Activation(
    Guid ActorId,
    ProjectFacts Project,
    ProjectBaseline Candidate,
    ProjectBaseline? Prior,
    IReadOnlyList<ScheduleActivity> Activities,
    IReadOnlyList<ScheduleDependency> Dependencies,
    IReadOnlyList<ProjectMilestone> Milestones,
    bool ApprovalRequired,
    DateTimeOffset Now);
