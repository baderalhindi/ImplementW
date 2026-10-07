using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// WF-03's baselines (TASK-046). A candidate is opened from the working schedule, one at a time per project, and submitted:
/// to WF-11 where the governance profile requires baseline approval, or straight to ACTIVE where it does not (ADR-015).
/// <see cref="EventHandlers.BaselineApprovalOutcomeHandler"/> applies WF-11's outcome; <see cref="BaselineActivation"/> makes a
/// baseline ACTIVE and supersedes the one before in the same transaction. Each write holds the project's schedule lock.
/// </summary>
internal sealed class BaselineService(
    IScheduleRepository repository,
    IProjectFactsReader projects,
    ScheduleAccess access,
    SchedulePolicy policy,
    RebaselineAuthorization rebaselines,
    BaselineActivation activation,
    IApprovalRequests approvals,
    IAuditTrail audit,
    TimeProvider timeProvider) : IBaselineService
{
    public async Task<ProjectBaselinePage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!(await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false)))
        {
            return new ProjectBaselinePage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ProjectBaseline> items, int total) = await repository.PageBaselinesAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        return new ProjectBaselinePage([.. items.Select(ScheduleMapping.ToDetail)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ProjectBaselineDetail>>> GetAsync(Guid callerId, Guid baselineId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ScheduleView, baselineId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : Versioned(loaded.Baseline!);
    }

    public async Task<AdministrationResult<Versioned<ProjectBaselineDetail>>> CreateAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectFacts? project = await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
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
        ProjectSchedule? schedule = await repository.LockScheduleAsync(project.Id, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AdministrationError.Rule(ScheduleErrorCodes.NotInitialized);
        }

        IReadOnlyList<ProjectBaseline> baselines = await repository.ListBaselinesAsync(project.Id, track: false, cancellationToken).ConfigureAwait(false);
        if (baselines.Any(b => BaselineWorkflow.IsOpen(b.Status)))
        {
            return AdministrationError.Conflict(ScheduleErrorCodes.BaselineCandidateExists);
        }

        if (ScheduleCalculation.PlannedFinish(await repository.ListActivitiesAsync(schedule.Id, track: false, cancellationToken).ConfigureAwait(false)) is not { } finish)
        {
            return AdministrationError.Rule(ScheduleErrorCodes.BaselineEmpty);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectBaseline baseline = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            BaselineType = BaselineType.Approved,
            VersionNo = baselines.Count == 0 ? 1 : baselines.Max(b => b.VersionNo) + 1,
            RevisionNo = 1,
            Status = ProjectBaselineStatus.Draft,
            BaselineFinishDate = finish,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(baseline);
        audit.Stage(ScheduleAudit.BaselineCreated(callerId, project, baseline));
        switch (await repository.SaveAsync(cancellationToken).ConfigureAwait(false))
        {
            case ScheduleSaveOutcome.Saved:
                await work.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Versioned(baseline);
            case ScheduleSaveOutcome.Duplicate:
                return AdministrationError.Conflict(ScheduleErrorCodes.BaselineCandidateExists);
            case ScheduleSaveOutcome.ConcurrencyConflict:
                return AdministrationError.PreconditionFailed;
            default:
                throw new InvalidOperationException("Unknown save outcome.");
        }
    }

    public async Task<AdministrationResult<Versioned<ProjectBaselineDetail>>> SubmitAsync(
        Guid callerId, Guid baselineId, BaselineSubmission submission, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);

        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ScheduleEdit, baselineId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ProjectBaseline baseline) = (loaded.Project!, loaded.Baseline!);
        if (baseline.Status is not (ProjectBaselineStatus.Draft or ProjectBaselineStatus.Returned))
        {
            return AdministrationError.InvalidTransition;
        }

        if (ScheduleService.NotEligible(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        ProjectSchedule schedule = await repository.LockScheduleAsync(project.Id, cancellationToken).ConfigureAwait(false)
                                   ?? throw new InvalidOperationException($"Baseline {baseline.Id} has no schedule to activate from.");
        IReadOnlyList<ScheduleActivity> activities = await repository.ListActivitiesAsync(schedule.Id, track: false, cancellationToken).ConfigureAwait(false);
        if (ScheduleCalculation.PlannedFinish(activities) is not { } finish)
        {
            return AdministrationError.Rule(ScheduleErrorCodes.BaselineEmpty);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        bool approvalRequired = await policy.RequiresBaselineApprovalAsync(project.GovernanceProfileItemId, now, cancellationToken).ConfigureAwait(false);

        // Every rebaseline after the first APPROVED baseline implements an approved WF-08 change (BR-SCH-034), applied as it takes
        // effect: now, where no approval is required; on WF-11's approval otherwise, so only checked here. Superseding a Declared
        // Baseline (ADR-014) is the project's first approved plan, not a rebaseline.
        ProjectBaseline? active = await repository.FindActiveBaselineAsync(project.Id, track: true, cancellationToken).ConfigureAwait(false);
        if (active is { BaselineType: BaselineType.Approved })
        {
            if (submission.ChangeAuthorizationId is not { } authorizationId)
            {
                return AdministrationError.Rule(ScheduleErrorCodes.ChangeAuthorizationRequired, new FieldIssue("changeAuthorizationId", FieldIssue.Required));
            }

            baseline.ChangeAuthorizationId = authorizationId;
            bool authorized = approvalRequired
                ? await rebaselines.IsApplicableAsync(project, active, authorizationId, cancellationToken).ConfigureAwait(false)
                : await rebaselines.ApplyAsync(project, active, baseline, callerId, now, cancellationToken).ConfigureAwait(false);
            if (!authorized)
            {
                return AdministrationError.Rule(ScheduleErrorCodes.ChangeAuthorizationRequired, new FieldIssue("changeAuthorizationId", FieldIssue.NotAllowed));
            }
        }

        ProjectBaselineStatus from = baseline.Status;

        // A RETURNED candidate comes back as the next revision, which a new WF-11 run reviews (TASK-035 D-9).
        if (from == ProjectBaselineStatus.Returned)
        {
            baseline.RevisionNo++;
        }

        baseline.BaselineFinishDate = finish;
        baseline.UpdatedAt = now;
        baseline.UpdatedBy = callerId;

        if (!approvalRequired)
        {
            IReadOnlyList<ScheduleDependency> dependencies = await repository.ListDependenciesAsync(schedule.Id, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ProjectMilestone> milestones = await repository.ListMilestonesAsync(schedule.Id, cancellationToken).ConfigureAwait(false);
            return await ActivatedAsync(
                work, baseline,
                await activation.ActivateAsync(new Activation(callerId, project, baseline, active, activities, dependencies, milestones, ApprovalRequired: false, now), cancellationToken)
                    .ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }

        // The run is staged, not saved: it commits with SUBMITTED in the save below, or not at all (M-11). The requester is the
        // submitter, so WF-11's own rule keeps them from deciding it.
        AdministrationResult<ApprovalInstanceDetail> run = await approvals.StartAsync(
            new ApprovalStart(
                new ApprovalSubject(ScheduleApprovalRouting.SubjectModule, ScheduleApprovalRouting.SubjectType, baseline.Id, baseline.RevisionNo),
                ScheduleApprovalRouting.BaselineRoutingKey,
                callerId,
                project.Id,
                project.DepartmentId,
                project.GovernanceProfileItemId,
                null,
                null),
            cancellationToken).ConfigureAwait(false);
        if (!run.Succeeded)
        {
            return run.Error;
        }

        baseline.Status = ProjectBaselineStatus.Submitted;
        audit.Stage(ScheduleAudit.BaselineSubmitted(callerId, project, baseline, from, run.Value.Id));
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != ScheduleSaveOutcome.Saved)
        {
            return AdministrationError.PreconditionFailed;
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Versioned(baseline);
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid baselineId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ScheduleEdit, baselineId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ProjectBaseline baseline) = (loaded.Project!, loaded.Baseline!);
        if (baseline.Status != ProjectBaselineStatus.Draft)
        {
            return AdministrationError.Conflict(ScheduleErrorCodes.BaselineNotEditable);
        }

        audit.Stage(ScheduleAudit.BaselineDeleted(callerId, project, baseline));
        repository.Remove(baseline);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            ScheduleSaveOutcome.Saved => null,
            ScheduleSaveOutcome.ConcurrencyConflict or ScheduleSaveOutcome.Duplicate => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<BaselineActivityPage>> ListActivitiesAsync(Guid callerId, Guid baselineId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ScheduleView, baselineId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (IReadOnlyList<BaselineActivity> items, int total) = await repository.PageBaselineActivitiesAsync(baselineId, page, cancellationToken).ConfigureAwait(false);
        return new BaselineActivityPage([.. items.Select(ScheduleMapping.ToDetail)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<BaselineDependencyPage>> ListDependenciesAsync(Guid callerId, Guid baselineId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ScheduleView, baselineId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (IReadOnlyList<BaselineDependency> items, int total) = await repository.PageBaselineDependenciesAsync(baselineId, page, cancellationToken).ConfigureAwait(false);
        return new BaselineDependencyPage([.. items.Select(ScheduleMapping.ToDetail)], page.Page, page.PageSize, total);
    }

    /// <summary>Commits an activation that saved; the database's single-ACTIVE index refusing it is 409, a stale row 412.</summary>
    private async Task<AdministrationResult<Versioned<ProjectBaselineDetail>>> ActivatedAsync(
        IScheduleWork work, ProjectBaseline baseline, ScheduleSaveOutcome outcome, CancellationToken cancellationToken)
    {
        switch (outcome)
        {
            case ScheduleSaveOutcome.Saved:
                await work.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Versioned(baseline);
            case ScheduleSaveOutcome.Duplicate:
                return AdministrationError.Conflict(ScheduleErrorCodes.SingleActiveBaseline);
            case ScheduleSaveOutcome.ConcurrencyConflict:
                return AdministrationError.PreconditionFailed;
            default:
                throw new InvalidOperationException("Unknown save outcome.");
        }
    }

    private async Task<Loaded> LoadAsync(Guid callerId, string permissionCode, Guid baselineId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectBaseline? baseline = await repository.FindBaselineAsync(baselineId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = baseline is null ? null : await projects.FindAsync(baseline.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded(null, null, AdministrationError.NotFound)
            : new Loaded(project, baseline, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    private Versioned<ProjectBaselineDetail> Versioned(ProjectBaseline baseline) => new(ScheduleMapping.ToDetail(baseline), repository.RowVersionOf(baseline));

    private sealed record Loaded(ProjectFacts? Project, ProjectBaseline? Baseline, AdministrationError? Error);
}
