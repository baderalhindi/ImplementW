using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// WF-03 records a milestone ACHIEVED when WF-05 accepts an achievement of it (edge 10, ICD-04). It runs in WF-05's outcome
/// dispatch, inside that transaction, and stages the change for WF-05's save: the acceptance and the status commit together.
/// </summary>
internal sealed class MilestoneAchievementRecorder(IScheduleRepository repository, IProjectFactsReader projects, IAuditTrail audit, TimeProvider timeProvider)
    : IMilestoneAchievementRecorder
{
    public async Task<bool> RecordAchievedAsync(Guid projectMilestoneId, Guid actorId, Guid milestoneAchievementId, CancellationToken cancellationToken)
    {
        ProjectMilestone found = await repository.ReadMilestoneAsync(projectMilestoneId, cancellationToken).ConfigureAwait(false)
                                 ?? throw new InvalidOperationException($"No milestone {projectMilestoneId} to record achieved.");
        ProjectFacts project = await projects.FindAsync(found.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Milestone {projectMilestoneId} names no project.");

        // The schedule lock orders this after any cancellation in flight; the milestone is read again under it.
        _ = await repository.LockScheduleAsync(project.Id, cancellationToken).ConfigureAwait(false);
        ProjectMilestone milestone = (await repository.FindMilestoneAsync(projectMilestoneId, null, cancellationToken).ConfigureAwait(false))!;
        if (milestone.Status != ProjectMilestoneStatus.Planned)
        {
            return milestone.Status == ProjectMilestoneStatus.Achieved;
        }

        milestone.Status = ProjectMilestoneStatus.Achieved;
        milestone.UpdatedAt = timeProvider.GetUtcNow();
        milestone.UpdatedBy = actorId;
        audit.Stage(ScheduleAudit.MilestoneAchieved(actorId, project, milestone, milestoneAchievementId));
        return true;
    }
}
