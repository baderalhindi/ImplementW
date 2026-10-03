using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// WF-03's side of the shared milestone (ICD-04, TASK-050). Every write locks the project's schedule row, as the schedule's
/// other writes do, so a milestone changes one write at a time with the plan and the baselines. While a baseline candidate is
/// with WF-11 the milestones are frozen with the rest of the plan: the candidate copies their dates as it activates.
/// </summary>
internal sealed class ProjectMilestoneService(
    IScheduleRepository repository,
    IProjectFactsReader projects,
    ScheduleAccess access,
    IMasterDataResolver masterData,
    IAuditTrail audit,
    TimeProvider timeProvider) : IProjectMilestoneService
{
    public async Task<ProjectMilestonePage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!(await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false)))
        {
            return new ProjectMilestonePage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ProjectMilestone> items, int total) = await repository.PageMilestonesAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        ActiveMilestoneBaseline? baseline = await ActiveBaselineAsync(projectId, cancellationToken).ConfigureAwait(false);
        return new ProjectMilestonePage([.. items.Select(m => ScheduleMapping.ToDetail(m, baseline))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ProjectMilestoneDetail>>> GetAsync(Guid callerId, Guid milestoneId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ScheduleView, milestoneId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(loaded.Milestone!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProjectMilestoneDetail>>> CreateAsync(Guid callerId, ProjectMilestoneDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        ProjectFacts? project = await projects.FindAsync(draft.ProjectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.ScheduleEdit, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (ScheduleService.NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Opened opened = await OpenAsync(project, cancellationToken).ConfigureAwait(false);
        if (opened.Error is { } closed)
        {
            return closed;
        }

        ProjectSchedule schedule = opened.Schedule!;
        if (await ReferencesRefusedAsync(schedule, draft.ScheduleActivityId, draft.MilestoneCategoryItemId, cancellationToken).ConfigureAwait(false) is { } badReference)
        {
            return badReference;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectMilestone milestone = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ProjectScheduleId = schedule.Id,
            ScheduleActivityId = draft.ScheduleActivityId,
            Title = draft.Title,
            MilestoneCategoryItemId = draft.MilestoneCategoryItemId,
            ForecastDate = draft.ForecastDate,
            Status = ProjectMilestoneStatus.Planned,
            SortOrder = draft.SortOrder,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(milestone);
        audit.Stage(ScheduleAudit.MilestoneCreated(callerId, project, milestone));
        return await SaveAsync(work, milestone, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProjectMilestoneDetail>>> UpdateAsync(
        Guid callerId, Guid milestoneId, ProjectMilestoneChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ScheduleEdit, milestoneId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ProjectMilestone milestone) = (loaded.Project!, loaded.Milestone!);
        if (ScheduleService.NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Opened opened = await OpenAsync(project, cancellationToken).ConfigureAwait(false);
        if (opened.Error is { } closed)
        {
            return closed;
        }

        // An ACHIEVED or CANCELLED milestone is history: WF-05's acceptance rests on it as it was.
        if (milestone.Status != ProjectMilestoneStatus.Planned)
        {
            return AdministrationError.Conflict(ScheduleErrorCodes.NotEditable);
        }

        if (await ReferencesRefusedAsync(opened.Schedule!, changes.ScheduleActivityId, changes.MilestoneCategoryItemId, cancellationToken).ConfigureAwait(false) is { } badReference)
        {
            return badReference;
        }

        MilestoneInputs before = MilestoneInputs.Of(milestone);
        milestone.ScheduleActivityId = changes.ScheduleActivityId;
        milestone.Title = changes.Title;
        milestone.MilestoneCategoryItemId = changes.MilestoneCategoryItemId;
        milestone.ForecastDate = changes.ForecastDate;
        milestone.SortOrder = changes.SortOrder;
        Touch(milestone, callerId);
        audit.Stage(ScheduleAudit.MilestoneChanged(callerId, project, before, milestone));
        return await SaveAsync(work, milestone, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProjectMilestoneDetail>>> CancelAsync(Guid callerId, Guid milestoneId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ScheduleEdit, milestoneId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ProjectMilestone milestone) = (loaded.Project!, loaded.Milestone!);
        if (ScheduleService.NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Opened opened = await OpenAsync(project, cancellationToken).ConfigureAwait(false);
        if (opened.Error is { } closed)
        {
            return closed;
        }

        if (milestone.Status != ProjectMilestoneStatus.Planned)
        {
            return AdministrationError.InvalidTransition;
        }

        milestone.Status = ProjectMilestoneStatus.Cancelled;
        Touch(milestone, callerId);
        audit.Stage(ScheduleAudit.MilestoneCancelled(callerId, project, milestone));
        return await SaveAsync(work, milestone, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<BaselineMilestonePage>> ListBaselineMilestonesAsync(Guid callerId, Guid baselineId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ProjectBaseline? baseline = await repository.FindBaselineAsync(baselineId, null, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = baseline is null ? null : await projects.FindAsync(baseline.ProjectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.ScheduleView, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        (IReadOnlyList<BaselineMilestone> items, int total) = await repository.PageBaselineMilestonesAsync(baselineId, page, cancellationToken).ConfigureAwait(false);
        return new BaselineMilestonePage([.. items.Select(ScheduleMapping.ToDetail)], page.Page, page.PageSize, total);
    }

    /// <summary>
    /// Locks the project's schedule. Refused when there is none, and while a baseline candidate is with WF-11, because the
    /// candidate will copy the milestones' dates as they stand when it activates.
    /// </summary>
    private async Task<Opened> OpenAsync(ProjectFacts project, CancellationToken cancellationToken)
    {
        ProjectSchedule? schedule = await repository.LockScheduleAsync(project.Id, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return new Opened(null, AdministrationError.Rule(ScheduleErrorCodes.NotInitialized));
        }

        IReadOnlyList<ProjectBaseline> baselines = await repository.ListBaselinesAsync(project.Id, track: false, cancellationToken).ConfigureAwait(false);
        return baselines.Any(b => b.Status == ProjectBaselineStatus.Submitted)
            ? new Opened(null, AdministrationError.Conflict(ScheduleErrorCodes.NotEditable))
            : new Opened(schedule, null);
    }

    /// <summary>The activity, if named, is a live activity of this schedule; the category is a PUBLISHED MILESTONE_CATEGORY item.</summary>
    private async Task<AdministrationError?> ReferencesRefusedAsync(ProjectSchedule schedule, Guid? scheduleActivityId, Guid categoryItemId, CancellationToken cancellationToken)
    {
        if (scheduleActivityId is { } activityId
            && !(await repository.ReadActivityAsync(activityId, cancellationToken).ConfigureAwait(false) is ({ } activity, _)
                 && activity.ProjectScheduleId == schedule.Id
                 && activity.Status != ScheduleActivityStatus.Cancelled))
        {
            return AdministrationError.Rule(ScheduleErrorCodes.MilestoneActivityInvalid, new FieldIssue("scheduleActivityId", FieldIssue.NotFound));
        }

        try
        {
            await masterData.RequirePublishedItemAsync(MasterDataCatalogueCodes.MilestoneCategory, categoryItemId, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (ConfigurationMissingException)
        {
            return AdministrationError.Rule(ScheduleErrorCodes.MilestoneCategoryInvalid, new FieldIssue("milestoneCategoryItemId", FieldIssue.NotFound));
        }
    }

    /// <summary>Saves and commits; a stale row is 412.</summary>
    private async Task<AdministrationResult<Versioned<ProjectMilestoneDetail>>> SaveAsync(IScheduleWork work, ProjectMilestone milestone, CancellationToken cancellationToken)
    {
        switch (await repository.SaveAsync(cancellationToken).ConfigureAwait(false))
        {
            case ScheduleSaveOutcome.Saved:
                await work.CommitAsync(cancellationToken).ConfigureAwait(false);
                return await VersionedAsync(milestone, cancellationToken).ConfigureAwait(false);
            case ScheduleSaveOutcome.ConcurrencyConflict or ScheduleSaveOutcome.Duplicate:
                return AdministrationError.PreconditionFailed;
            default:
                throw new InvalidOperationException("Unknown save outcome.");
        }
    }

    private async Task<Versioned<ProjectMilestoneDetail>> VersionedAsync(ProjectMilestone milestone, CancellationToken cancellationToken) =>
        new(ScheduleMapping.ToDetail(milestone, await ActiveBaselineAsync(milestone.ProjectId, cancellationToken).ConfigureAwait(false)), repository.RowVersionOf(milestone));

    private async Task<ActiveMilestoneBaseline?> ActiveBaselineAsync(Guid projectId, CancellationToken cancellationToken) =>
        await repository.FindActiveBaselineAsync(projectId, track: false, cancellationToken).ConfigureAwait(false) is { } baseline
            ? new ActiveMilestoneBaseline(baseline.Id, (await repository.ListBaselineMilestonesAsync(baseline.Id, cancellationToken).ConfigureAwait(false)).ToDictionary(m => m.ProjectMilestoneId))
            : null;

    private async Task<Loaded> LoadAsync(Guid callerId, string permissionCode, Guid milestoneId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectMilestone? milestone = await repository.FindMilestoneAsync(milestoneId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = milestone is null ? null : await projects.FindAsync(milestone.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded(null, null, AdministrationError.NotFound)
            : new Loaded(project, milestone, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    private void Touch(ProjectMilestone milestone, Guid by)
    {
        milestone.UpdatedAt = timeProvider.GetUtcNow();
        milestone.UpdatedBy = by;
    }

    private sealed record Loaded(ProjectFacts? Project, ProjectMilestone? Milestone, AdministrationError? Error);

    private sealed record Opened(ProjectSchedule? Schedule, AdministrationError? Error);
}
