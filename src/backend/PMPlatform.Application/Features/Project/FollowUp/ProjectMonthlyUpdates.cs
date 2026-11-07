using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.FollowUp;

/// <inheritdoc cref="IProjectMonthlyUpdates"/>
internal sealed class ProjectMonthlyUpdates(IProjectRepository repository, IAuditTrail audit, TimeProvider timeProvider) : IProjectMonthlyUpdates
{
    public async Task<ProjectMonthlySnapshot?> ReadAsync(Guid projectId, CancellationToken cancellationToken) =>
        await repository.ReadAsync(projectId, cancellationToken).ConfigureAwait(false) is { } p
            ? new ProjectMonthlySnapshot(
                p.Id, p.FormalProjectId, p.LegacyReference, p.Title, p.DepartmentId, p.ExternalEntityId, p.LifecycleState, p.EntityContactUserId, p.DeclaredPlannedPercent,
                p.DeclaredActualPercent, p.DeclaredPlannedSpendPercent, p.DeclaredActualSpendPercent, p.CurrentObstacle, p.NextAction, p.NextActionDueDate)
            : null;

    public async Task<bool> StageAsync(Guid projectId, ProjectMonthlyUpdateValues values, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        ProjectEntity? project = await repository.FindAsync(projectId, null, cancellationToken).ConfigureAwait(false);
        if (project is null || project.LifecycleState == ProjectLifecycleState.Closed)
        {
            return false;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectFollowUpDetail before = ProjectMapping.ToFollowUp(project);
        project.DeclaredActualPercent = values.ActualProgressPercent;
        project.DeclaredActualSpendPercent = values.ActualSpendPercent ?? project.DeclaredActualSpendPercent;
        project.CurrentObstacle = values.CurrentObstacle;
        project.NextAction = values.NextAction;
        project.NextActionDueDate = values.NextActionDueDate;
        project.DeclaredAsOf = DateOnly.FromDateTime(now.UtcDateTime);
        project.DeclaredSource = DeclaredValueSource.EntityUpdate;
        project.UpdatedAt = now;
        project.UpdatedBy = actorId;
        audit.Stage(ProjectAudit.FollowUpChanged(actorId, before, project));
        if (values.Note is { } note)
        {
            repository.Add(new ProjectFollowUpNote
            {
                Id = Guid.CreateVersion7(now),
                ProjectId = project.Id,
                Note = note,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        return true;
    }

    public async Task StagePublishedProgressAsync(Guid projectId, decimal actualPercent, Guid actorId, CancellationToken cancellationToken)
    {
        if (await repository.FindAsync(projectId, null, cancellationToken).ConfigureAwait(false) is not { } project)
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectFollowUpDetail before = ProjectMapping.ToFollowUp(project);
        project.DeclaredActualPercent = actualPercent;
        project.DeclaredAsOf = DateOnly.FromDateTime(now.UtcDateTime);
        project.DeclaredSource = DeclaredValueSource.ProgressUpdate;
        project.UpdatedAt = now;
        project.UpdatedBy = actorId;
        audit.Stage(ProjectAudit.FollowUpChanged(actorId, before, project));
    }

    public async Task StageEntityContactAsync(Guid projectId, Guid contactUserId, Guid actorId, CancellationToken cancellationToken)
    {
        if (await repository.FindAsync(projectId, null, cancellationToken).ConfigureAwait(false) is { } project)
        {
            project.EntityContactUserId = contactUserId;
            project.UpdatedAt = timeProvider.GetUtcNow();
            project.UpdatedBy = actorId;
        }
    }
}
