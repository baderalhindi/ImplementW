using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.FollowUp;

/// <inheritdoc cref="IProjectMonthlyUpdates"/>
internal sealed class ProjectMonthlyUpdates(IProjectRepository repository, IConfigurationResolver resolver, IAuditTrail audit, TimeProvider timeProvider) : IProjectMonthlyUpdates
{
    public async Task<ProjectMonthlySnapshot?> ReadAsync(Guid projectId, CancellationToken cancellationToken) =>
        await repository.ReadAsync(projectId, cancellationToken).ConfigureAwait(false) is { } p ? await SnapshotAsync(p, cancellationToken).ConfigureAwait(false) : null;

    private async Task<ProjectMonthlySnapshot> SnapshotAsync(ProjectEntity p, CancellationToken cancellationToken) =>
        new(
            p.Id, p.FormalProjectId, p.LegacyReference, p.Title, p.DepartmentId, p.ExternalEntityId, p.LifecycleState, p.EntityContactUserId, p.DeclaredPlannedPercent,
            p.DeclaredActualPercent, p.DeclaredPlannedSpendPercent, p.DeclaredActualSpendPercent, p.CurrentObstacle, p.NextAction, p.NextActionDueDate)
        {
            Stages = await StagesAsync(p, cancellationToken).ConfigureAwait(false),
            CurrentStageCode = p.CurrentStageCode,
            CurrentStagePercent = p.CurrentStagePercent,
        };

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
        IReadOnlyList<ProjectMonthlyStage> stages = await StagesAsync(project, cancellationToken).ConfigureAwait(false);
        int current = values.CurrentStageCode is { } code ? stages.ToList().FindIndex(s => s.StageCode == code) : -1;
        if (current >= 0 && values.CurrentStagePercent is { } stagePercent)
        {
            // The stages before the current one are complete, the current one at its %, the rest not started.
            project.DeclaredActualPercent = StageProgress.Overall(stages.Select((s, i) => (s.WeightPercent, (decimal?)(i < current ? 100m : i == current ? stagePercent : 0m))));
            project.DeclaredPlannedPercent = StageProgress.Overall(stages.Select(s => (s.WeightPercent, s.PlannedPercent))) ?? project.DeclaredPlannedPercent;
            project.CurrentStageCode = stages[current].StageCode;
            project.CurrentStagePercent = stagePercent;
        }
        else if (values.ActualProgressPercent is { } overall)
        {
            project.DeclaredActualPercent = overall;
        }
        else
        {
            // A stage the plan does not have, and no overall figure: nothing to write.
            return false;
        }

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

    public async Task StagePublishedProgressAsync(Guid projectId, PublishedProgressValues values, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (await repository.FindAsync(projectId, null, cancellationToken).ConfigureAwait(false) is not { } project)
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectFollowUpDetail before = ProjectMapping.ToFollowUp(project);
        project.DeclaredActualPercent = values.ActualPercent;
        if (values.IsStageBased)
        {
            project.DeclaredPlannedPercent = values.PlannedPercent;
            project.CurrentStageCode = values.CurrentStageCode;
            project.CurrentStagePercent = values.CurrentStageCode is null ? null : values.CurrentStagePercent;
        }

        project.DeclaredAsOf = DateOnly.FromDateTime(now.UtcDateTime);
        project.DeclaredSource = DeclaredValueSource.ProgressUpdate;
        project.UpdatedAt = now;
        project.UpdatedBy = actorId;
        audit.Stage(ProjectAudit.FollowUpChanged(actorId, before, project));
    }

    /// <summary>The project's plan, the stages that apply in stage order, each with its weight in force and planned % today; empty without a plan.</summary>
    private async Task<IReadOnlyList<ProjectMonthlyStage>> StagesAsync(ProjectEntity project, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (project.StagePlanRevisionNo == 0 || await resolver.ResolveStagesAsync(now, cancellationToken).ConfigureAwait(false) is not { } stages)
        {
            return [];
        }

        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        ILookup<Guid, ProjectStagePlanEntry> plans = await repository.ReadStagePlansAsync([project.Id], cancellationToken).ConfigureAwait(false);
        List<ProjectStagePlanEntry> plan = [.. plans[project.Id]];
        return [.. stages.Stages
            .Select(s => (Stage: s, Plan: plan.FirstOrDefault(e => e.StageCode == s.Code)))
            .Where(x => x.Plan?.Applies ?? true)
            .Select(x => new ProjectMonthlyStage(
                x.Stage.Code, x.Stage.Name, x.Stage.WeightPercent,
                x.Plan is { PlannedStartDate: { } start, PlannedEndDate: { } end } ? StageProgress.PlannedPercent(start, end, today) : null))];
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
