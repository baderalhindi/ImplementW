using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Graphs;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// WF-03's working schedule (TASK-046). Every write locks the project's schedule row, checks the change against the whole
/// schedule as it now stands, recalculates every date (<see cref="ScheduleCalculation"/>) and the live Schedule Health, and
/// saves it all in one transaction. While a baseline candidate is with WF-11 the plan it was submitted from is frozen; the
/// forecast is not. No write here touches a baseline.
/// </summary>
internal sealed class ScheduleService(
    IScheduleRepository repository,
    IProjectFactsReader projects,
    ScheduleAccess access,
    ScheduleHealthProjection health,
    IAuditTrail audit,
    TimeProvider timeProvider) : IScheduleService
{
    public async Task<ProjectSchedulePage> ListSchedulesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ProjectSchedule? schedule = await ReachesAsync(callerId, projectId, cancellationToken).ConfigureAwait(false)
            ? await repository.FindScheduleAsync(projectId, cancellationToken).ConfigureAwait(false)
            : null;
        if (schedule is null || page.Page != 1)
        {
            return new ProjectSchedulePage([], page.Page, page.PageSize, schedule is null ? 0 : 1);
        }

        ProjectBaseline? active = await repository.FindActiveBaselineAsync(projectId, track: false, cancellationToken).ConfigureAwait(false);
        return new ProjectSchedulePage([ScheduleMapping.ToDetail(schedule, active?.Id)], page.Page, page.PageSize, 1);
    }

    public async Task<AdministrationResult<Versioned<ProjectScheduleDetail>>> InitializeAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken)
    {
        Reached reached = await ReachAsync(callerId, PermissionCatalogue.ScheduleEdit, projectId, cancellationToken).ConfigureAwait(false);
        if (reached.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = reached.Project!;
        if (NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        if (await repository.FindScheduleAsync(project.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return AdministrationError.Conflict(ScheduleErrorCodes.Exists);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectSchedule schedule = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(schedule);
        audit.Stage(ScheduleAudit.Initialized(callerId, project, schedule));

        // An intake project may already hold its Declared Baseline (ADR-014).
        ProjectBaseline? active = await repository.FindActiveBaselineAsync(project.Id, track: false, cancellationToken).ConfigureAwait(false);
        await health.StageAsync(callerId, project, [], active, now, cancellationToken).ConfigureAwait(false);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            ScheduleSaveOutcome.Saved => new Versioned<ProjectScheduleDetail>(ScheduleMapping.ToDetail(schedule, active?.Id), repository.RowVersionOf(schedule)),
            ScheduleSaveOutcome.Duplicate => AdministrationError.Conflict(ScheduleErrorCodes.Exists),
            ScheduleSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<ScheduleActivityPage> ListActivitiesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ProjectSchedule? schedule = await ReachesAsync(callerId, projectId, cancellationToken).ConfigureAwait(false)
            ? await repository.FindScheduleAsync(projectId, cancellationToken).ConfigureAwait(false)
            : null;
        if (schedule is null)
        {
            return new ScheduleActivityPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ScheduleActivity> items, int total) = await repository.PageActivitiesAsync(schedule.Id, page, cancellationToken).ConfigureAwait(false);
        ActiveBaseline? baseline = await ActiveBaselineAsync(projectId, cancellationToken).ConfigureAwait(false);
        return new ScheduleActivityPage([.. items.Select(a => ScheduleMapping.ToDetail(a, projectId, baseline))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> GetActivityAsync(Guid callerId, Guid activityId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadActivityAsync(callerId, PermissionCatalogue.ScheduleView, activityId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(loaded.Activity!, loaded.Project!.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> CreateActivityAsync(Guid callerId, ScheduleActivityDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Reached reached = await ReachAsync(callerId, PermissionCatalogue.ScheduleEdit, draft.ProjectId, cancellationToken).ConfigureAwait(false);
        if (reached.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = reached.Project!;
        if (NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Opened opened = await OpenAsync(project, cancellationToken).ConfigureAwait(false);
        if (opened.Error is { } closed)
        {
            return closed;
        }

        Plan plan = opened.Plan!;
        if (WbsCodeTaken(plan, draft.WbsCode, null) is { } taken)
        {
            return taken;
        }

        if (draft.ParentActivityId is { } parentId && ParentRefused(plan, parentId, null) is { } badParent)
        {
            return badParent;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ScheduleActivity activity = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectScheduleId = plan.Schedule.Id,
            ParentActivityId = draft.ParentActivityId,
            WbsCode = draft.WbsCode,
            Name = draft.Name,
            ActivityKind = ScheduleActivityKind.Activity,
            RequestedStartDate = draft.RequestedStartDate,
            PlannedStartDate = draft.RequestedStartDate,
            PlannedFinishDate = WorkingDays.FinishOf(draft.RequestedStartDate, draft.PlannedDurationDays),
            PlannedDurationDays = draft.PlannedDurationDays,
            Status = ScheduleActivityStatus.Planned,
            SortOrder = draft.SortOrder,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(activity);
        plan.Activities.Add(activity);

        // A new leaf's forecast starts as its plan, also once the project is under baseline control.
        int recalculated = Recalculate(plan, a => plan.Active is null || a.Id == activity.Id);
        audit.Stage(ScheduleAudit.ActivityCreated(callerId, project, activity, recalculated));
        return await SaveActivityAsync(work, plan, project, callerId, activity, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> UpdateActivityAsync(
        Guid callerId, Guid activityId, ScheduleActivityChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Loaded loaded = await LoadActivityAsync(callerId, PermissionCatalogue.ScheduleEdit, activityId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        if (NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Opened opened = await OpenAsync(project, cancellationToken).ConfigureAwait(false);
        if (opened.Error is { } closed)
        {
            return closed;
        }

        Plan plan = opened.Plan!;
        ScheduleActivity activity = plan.Activities.Single(a => a.Id == activityId);
        if (activity.Status == ScheduleActivityStatus.Cancelled)
        {
            return AdministrationError.Conflict(ScheduleErrorCodes.NotEditable);
        }

        if (WbsCodeTaken(plan, changes.WbsCode, activity.Id) is { } taken)
        {
            return taken;
        }

        if (changes.ParentActivityId is { } parentId && parentId != activity.ParentActivityId && ParentRefused(plan, parentId, activity) is { } badParent)
        {
            return badParent;
        }

        ActivityInputs before = ActivityInputs.Of(activity);
        activity.ParentActivityId = changes.ParentActivityId;
        activity.WbsCode = changes.WbsCode;
        activity.Name = changes.Name;
        activity.RequestedStartDate = changes.RequestedStartDate;
        activity.PlannedDurationDays = changes.PlannedDurationDays;
        activity.SortOrder = changes.SortOrder;
        DateTimeOffset now = timeProvider.GetUtcNow();
        Touch(activity, callerId, now);

        int recalculated = Recalculate(plan, _ => plan.Active is null);
        audit.Stage(ScheduleAudit.ActivityChanged(callerId, project, before, activity, recalculated));
        return await SaveActivityAsync(work, plan, project, callerId, activity, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> CancelActivityAsync(Guid callerId, Guid activityId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadActivityAsync(callerId, PermissionCatalogue.ScheduleEdit, activityId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        if (NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Opened opened = await OpenAsync(project, cancellationToken).ConfigureAwait(false);
        if (opened.Error is { } closed)
        {
            return closed;
        }

        Plan plan = opened.Plan!;
        ScheduleActivity activity = plan.Activities.Single(a => a.Id == activityId);
        if (activity.Status != ScheduleActivityStatus.Planned)
        {
            return AdministrationError.InvalidTransition;
        }

        if (plan.Activities.Any(a => a.ParentActivityId == activity.Id && a.Status != ScheduleActivityStatus.Cancelled)
            || plan.Dependencies.Any(d => d.PredecessorActivityId == activity.Id || d.SuccessorActivityId == activity.Id))
        {
            return AdministrationError.Conflict(ScheduleErrorCodes.ActivityInUse);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        activity.Status = ScheduleActivityStatus.Cancelled;
        Touch(activity, callerId, now);
        int recalculated = Recalculate(plan, _ => plan.Active is null);
        audit.Stage(ScheduleAudit.ActivityCancelled(callerId, project, activity, recalculated));
        return await SaveActivityAsync(work, plan, project, callerId, activity, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> ReforecastActivityAsync(
        Guid callerId, Guid activityId, ScheduleForecast forecast, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(forecast);

        Loaded loaded = await LoadActivityAsync(callerId, PermissionCatalogue.ScheduleEdit, activityId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        if (NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        // The forecast is not frozen with a submitted candidate: it is never part of a baseline (BR-SCH-031).
        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Opened opened = await OpenAsync(project, cancellationToken, frozenRefuses: false).ConfigureAwait(false);
        if (opened.Error is { } closed)
        {
            return closed;
        }

        Plan plan = opened.Plan!;
        if (plan.Active is null)
        {
            return AdministrationError.Rule(ScheduleErrorCodes.ActiveBaselineRequired);
        }

        ScheduleActivity activity = plan.Activities.Single(a => a.Id == activityId);
        if (!ScheduleCalculation.IsLiveLeaf(activity))
        {
            return AdministrationError.Rule(ScheduleErrorCodes.ForecastInvalid, new FieldIssue("activityId", FieldIssue.NotAllowed));
        }

        ScheduleForecastDates before = new(activity.ForecastStartDate, activity.ForecastFinishDate);
        DateTimeOffset now = timeProvider.GetUtcNow();
        activity.ForecastStartDate = forecast.ForecastStartDate;
        activity.ForecastFinishDate = forecast.ForecastFinishDate;
        Touch(activity, callerId, now);
        Recalculate(plan, _ => false);
        audit.Stage(ScheduleAudit.ActivityReforecast(callerId, project, before, activity));
        return await SaveActivityAsync(work, plan, project, callerId, activity, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScheduleDependencyPage> ListDependenciesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ProjectSchedule? schedule = await ReachesAsync(callerId, projectId, cancellationToken).ConfigureAwait(false)
            ? await repository.FindScheduleAsync(projectId, cancellationToken).ConfigureAwait(false)
            : null;
        if (schedule is null)
        {
            return new ScheduleDependencyPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ScheduleDependency> items, int total) = await repository.PageDependenciesAsync(schedule.Id, page, cancellationToken).ConfigureAwait(false);
        return new ScheduleDependencyPage([.. items.Select(d => ScheduleMapping.ToDetail(d, projectId))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ScheduleDependencyDetail>>> CreateDependencyAsync(Guid callerId, ScheduleDependencyDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // The project is the predecessor's. An activity the caller cannot see is one that does not exist (R-47).
        ScheduleActivity? predecessor = await repository.FindActivityAsync(draft.PredecessorActivityId, null, cancellationToken).ConfigureAwait(false);
        ProjectSchedule? schedule = predecessor is null ? null : await repository.FindScheduleByIdAsync(predecessor.ProjectScheduleId, cancellationToken).ConfigureAwait(false);
        Reached reached = schedule is null
            ? new Reached(null, AdministrationError.NotFound)
            : await ReachAsync(callerId, PermissionCatalogue.ScheduleEdit, schedule.ProjectId, cancellationToken).ConfigureAwait(false);
        if (reached.Error is { Kind: AdministrationErrorKind.NotFound })
        {
            return AdministrationError.Rule(ScheduleErrorCodes.DependencyInvalid, new FieldIssue("predecessorActivityId", FieldIssue.NotFound));
        }

        if (reached.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = reached.Project!;
        if (NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Opened opened = await OpenAsync(project, cancellationToken).ConfigureAwait(false);
        if (opened.Error is { } closed)
        {
            return closed;
        }

        Plan plan = opened.Plan!;
        Dictionary<Guid, ScheduleActivity> byId = plan.Activities.ToDictionary(a => a.Id);
        List<FieldIssue> issues = [];
        foreach ((Guid id, string field) in new[] { (draft.PredecessorActivityId, "predecessorActivityId"), (draft.SuccessorActivityId, "successorActivityId") })
        {
            if (!byId.TryGetValue(id, out ScheduleActivity? end))
            {
                issues.Add(new FieldIssue(field, FieldIssue.NotFound));
            }
            else if (!ScheduleCalculation.IsLiveLeaf(end))
            {
                // A summary is no dependency end in the MVP (BR-SCH-026), and a cancelled activity is out of the plan.
                issues.Add(new FieldIssue(field, FieldIssue.NotAllowed));
            }
        }

        if (draft.PredecessorActivityId == draft.SuccessorActivityId)
        {
            issues.Add(new FieldIssue("successorActivityId", FieldIssue.NotAllowed));
        }

        if (issues.Count > 0)
        {
            return AdministrationError.Rule(ScheduleErrorCodes.DependencyInvalid, [.. issues]);
        }

        if (plan.Dependencies.Any(d => d.PredecessorActivityId == draft.PredecessorActivityId && d.SuccessorActivityId == draft.SuccessorActivityId))
        {
            return AdministrationError.Conflict(ScheduleErrorCodes.DependencyExists);
        }

        if (DependencyGraph.ClosesCycle(plan.Dependencies.Select(d => (d.PredecessorActivityId, d.SuccessorActivityId)), draft.PredecessorActivityId, draft.SuccessorActivityId))
        {
            return AdministrationError.Rule(ScheduleErrorCodes.DependencyCircular, new FieldIssue("successorActivityId", FieldIssue.NotAllowed));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ScheduleDependency dependency = new()
        {
            Id = Guid.CreateVersion7(now),
            PredecessorActivityId = draft.PredecessorActivityId,
            SuccessorActivityId = draft.SuccessorActivityId,
            DependencyType = draft.DependencyType,
            LagDays = draft.LagDays,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(dependency);
        plan.Dependencies.Add(dependency);
        int recalculated = Recalculate(plan, _ => plan.Active is null);
        audit.Stage(ScheduleAudit.DependencyCreated(callerId, project, dependency, recalculated));
        await health.StageAsync(callerId, project, plan.Activities, plan.Active, now, cancellationToken).ConfigureAwait(false);
        switch (await repository.SaveAsync(cancellationToken).ConfigureAwait(false))
        {
            case ScheduleSaveOutcome.Saved:
                await work.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new Versioned<ScheduleDependencyDetail>(ScheduleMapping.ToDetail(dependency, project.Id), repository.RowVersionOf(dependency));
            case ScheduleSaveOutcome.Duplicate:
                return AdministrationError.Conflict(ScheduleErrorCodes.DependencyExists);
            case ScheduleSaveOutcome.ConcurrencyConflict:
                return AdministrationError.PreconditionFailed;
            default:
                throw new InvalidOperationException("Unknown save outcome.");
        }
    }

    public async Task<AdministrationError?> DeleteDependencyAsync(Guid callerId, Guid dependencyId, CancellationToken cancellationToken)
    {
        ScheduleDependency? dependency = await repository.FindDependencyAsync(dependencyId, cancellationToken).ConfigureAwait(false);
        ScheduleActivity? successor = dependency is null ? null : await repository.FindActivityAsync(dependency.SuccessorActivityId, null, cancellationToken).ConfigureAwait(false);
        ProjectSchedule? schedule = successor is null ? null : await repository.FindScheduleByIdAsync(successor.ProjectScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AdministrationError.NotFound;
        }

        Reached reached = await ReachAsync(callerId, PermissionCatalogue.ScheduleEdit, schedule.ProjectId, cancellationToken).ConfigureAwait(false);
        if (reached.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = reached.Project!;
        if (NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Opened opened = await OpenAsync(project, cancellationToken).ConfigureAwait(false);
        if (opened.Error is { } closed)
        {
            return closed;
        }

        Plan plan = opened.Plan!;
        plan.Dependencies.RemoveAll(d => d.Id == dependencyId);
        repository.Remove(dependency!);
        DateTimeOffset now = timeProvider.GetUtcNow();
        int recalculated = Recalculate(plan, _ => plan.Active is null);
        audit.Stage(ScheduleAudit.DependencyDeleted(callerId, project, dependency!, recalculated));
        await health.StageAsync(callerId, project, plan.Activities, plan.Active, now, cancellationToken).ConfigureAwait(false);

        // A dependency is never updated, so a concurrency conflict is a concurrent delete: gone either way (R-40).
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != ScheduleSaveOutcome.Saved)
        {
            return AdministrationError.NotFound;
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    public async Task<ScheduleHealthStatusPage> ListHealthStatusesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ScheduleHealthStatus? status = await ReachesAsync(callerId, projectId, cancellationToken).ConfigureAwait(false)
            ? await repository.FindHealthStatusAsync(projectId, track: false, cancellationToken).ConfigureAwait(false)
            : null;
        return status is not null && page.Page == 1
            ? new ScheduleHealthStatusPage([ScheduleMapping.ToDetail(status)], page.Page, page.PageSize, 1)
            : new ScheduleHealthStatusPage([], page.Page, page.PageSize, status is null ? 0 : 1);
    }

    /// <summary>A schedule is planned while its project is APPROVED_PLANNED or ACTIVE (the spec's SCH-CC-03, §4 step 1).</summary>
    internal static AdministrationError? NotEligible(ProjectFacts project) =>
        project.Status is ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active ? null : AdministrationError.Rule(ScheduleErrorCodes.ProjectNotEligible);

    /// <summary>
    /// Locks the project's schedule and reads it whole, with the ACTIVE baseline. Refused when there is no schedule, and — for a
    /// change to the plan — while a candidate is with WF-11, because the candidate will be activated from this plan as it stands.
    /// </summary>
    private async Task<Opened> OpenAsync(ProjectFacts project, CancellationToken cancellationToken, bool frozenRefuses = true)
    {
        ProjectSchedule? schedule = await repository.LockScheduleAsync(project.Id, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return new Opened(null, AdministrationError.Rule(ScheduleErrorCodes.NotInitialized));
        }

        IReadOnlyList<ProjectBaseline> baselines = await repository.ListBaselinesAsync(project.Id, track: false, cancellationToken).ConfigureAwait(false);
        if (frozenRefuses && baselines.Any(b => b.Status == ProjectBaselineStatus.Submitted))
        {
            return new Opened(null, AdministrationError.Conflict(ScheduleErrorCodes.NotEditable));
        }

        List<ScheduleActivity> activities = [.. await repository.ListActivitiesAsync(schedule.Id, track: true, cancellationToken).ConfigureAwait(false)];
        List<ScheduleDependency> dependencies = [.. await repository.ListDependenciesAsync(schedule.Id, cancellationToken).ConfigureAwait(false)];
        return new Opened(new Plan(schedule, activities, dependencies, baselines.SingleOrDefault(b => b.Status == ProjectBaselineStatus.Active)), null);
    }

    private static AdministrationError? WbsCodeTaken(Plan plan, string wbsCode, Guid? self) =>
        plan.Activities.Any(a => a.Id != self && a.WbsCode == wbsCode)
            ? AdministrationError.Conflict(ScheduleErrorCodes.WbsCodeExists, new FieldIssue("wbsCode", FieldIssue.Duplicate))
            : null;

    /// <summary>
    /// A parent is a live activity of the same schedule that is no dependency end (it becomes a summary, and a summary is none,
    /// BR-SCH-026), and never the activity itself or one of its descendants (VAL-SCH-002, VAL-SCH-003).
    /// </summary>
    private static AdministrationError? ParentRefused(Plan plan, Guid parentId, ScheduleActivity? self)
    {
        Dictionary<Guid, ScheduleActivity> byId = plan.Activities.ToDictionary(a => a.Id);
        if (!byId.TryGetValue(parentId, out ScheduleActivity? parent) || parent.Status == ScheduleActivityStatus.Cancelled)
        {
            return AdministrationError.Rule(ScheduleErrorCodes.HierarchyInvalid, new FieldIssue("parentActivityId", FieldIssue.NotFound));
        }

        if (self is not null)
        {
            for (ScheduleActivity? ancestor = parent; ancestor is not null; ancestor = ancestor.ParentActivityId is { } up ? byId.GetValueOrDefault(up) : null)
            {
                if (ancestor.Id == self.Id)
                {
                    return AdministrationError.Rule(ScheduleErrorCodes.HierarchyCircular, new FieldIssue("parentActivityId", FieldIssue.NotAllowed));
                }
            }
        }

        return plan.Dependencies.Any(d => d.PredecessorActivityId == parentId || d.SuccessorActivityId == parentId)
            ? AdministrationError.Rule(ScheduleErrorCodes.HierarchyInvalid, new FieldIssue("parentActivityId", FieldIssue.NotAllowed))
            : null;
    }

    private static int Recalculate(Plan plan, Func<ScheduleActivity, bool> forecastFollowsPlan) =>
        ScheduleCalculation.Recalculate(plan.Activities, plan.Dependencies, forecastFollowsPlan).Count;

    /// <summary>Stages the live health, saves and commits; a WBS code taken by a concurrent request is 409, a stale row 412.</summary>
    private async Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> SaveActivityAsync(
        IScheduleWork work, Plan plan, ProjectFacts project, Guid callerId, ScheduleActivity activity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await health.StageAsync(callerId, project, plan.Activities, plan.Active, now, cancellationToken).ConfigureAwait(false);
        switch (await repository.SaveAsync(cancellationToken).ConfigureAwait(false))
        {
            case ScheduleSaveOutcome.Saved:
                await work.CommitAsync(cancellationToken).ConfigureAwait(false);
                return await VersionedAsync(activity, project.Id, cancellationToken).ConfigureAwait(false);
            case ScheduleSaveOutcome.Duplicate:
                return AdministrationError.Conflict(ScheduleErrorCodes.WbsCodeExists, new FieldIssue("wbsCode", FieldIssue.Duplicate));
            case ScheduleSaveOutcome.ConcurrencyConflict:
                return AdministrationError.PreconditionFailed;
            default:
                throw new InvalidOperationException("Unknown save outcome.");
        }
    }

    private async Task<Versioned<ScheduleActivityDetail>> VersionedAsync(ScheduleActivity activity, Guid projectId, CancellationToken cancellationToken) =>
        new(ScheduleMapping.ToDetail(activity, projectId, await ActiveBaselineAsync(projectId, cancellationToken).ConfigureAwait(false)), repository.RowVersionOf(activity));

    private async Task<ActiveBaseline?> ActiveBaselineAsync(Guid projectId, CancellationToken cancellationToken) =>
        await repository.FindActiveBaselineAsync(projectId, track: false, cancellationToken).ConfigureAwait(false) is { } baseline
            ? new ActiveBaseline(baseline, (await repository.ListBaselineActivitiesAsync(baseline.Id, cancellationToken).ConfigureAwait(false)).ToDictionary(a => a.ScheduleActivityId))
            : null;

    /// <summary>Whether the caller may view the project's schedule: a collection of a project they may not see is empty.</summary>
    private async Task<bool> ReachesAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false);

    private async Task<Reached> ReachAsync(Guid callerId, string permissionCode, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectFacts? project = await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        return project is null ? new Reached(null, AdministrationError.NotFound)
            : new Reached(project, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Loaded> LoadActivityAsync(Guid callerId, string permissionCode, Guid activityId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ScheduleActivity? activity = await repository.FindActivityAsync(activityId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectSchedule? schedule = activity is null ? null : await repository.FindScheduleByIdAsync(activity.ProjectScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return new Loaded(null, null, AdministrationError.NotFound);
        }

        Reached reached = await ReachAsync(callerId, permissionCode, schedule.ProjectId, cancellationToken).ConfigureAwait(false);
        return new Loaded(reached.Project, activity, reached.Error);
    }

    private static void Touch(ScheduleActivity activity, Guid by, DateTimeOffset now)
    {
        activity.UpdatedAt = now;
        activity.UpdatedBy = by;
    }

    private sealed record Reached(ProjectFacts? Project, AdministrationError? Error);

    private sealed record Loaded(ProjectFacts? Project, ScheduleActivity? Activity, AdministrationError? Error);

    private sealed record Opened(Plan? Plan, AdministrationError? Error);

    /// <summary>The locked schedule, read whole: every activity tracked, every dependency, and the ACTIVE baseline.</summary>
    private sealed record Plan(ProjectSchedule Schedule, List<ScheduleActivity> Activities, List<ScheduleDependency> Dependencies, ProjectBaseline? Active);
}
