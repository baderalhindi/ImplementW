using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Progress;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// WF-02 progress reporting and Overall Project Health (TASK-044). Each method that changes a submission's state is one
/// edge of <see cref="ProgressWorkflow"/> and checks that edge before it writes. Actual and planned progress are derived
/// (<see cref="ProgressRollup"/>), health is computed by <see cref="OverallHealthRule"/> only, and publishing adds a
/// snapshot without touching any earlier one.
/// </summary>
internal sealed class ProgressService(
    IProgressRepository repository,
    IProjectFactsReader projects,
    ProgressAccess access,
    ProgressPolicy policy,
    IProgressInputs inputs,
    IAuditTrail audit,
    TimeProvider timeProvider) : IProgressService
{
    public async Task<ReportingCyclePage> ListCyclesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await ReachesAsync(callerId, projectId, cancellationToken).ConfigureAwait(false))
        {
            return new ReportingCyclePage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ReportingCycle> items, int total) = await repository.PageCyclesAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        return new ReportingCyclePage([.. items.Select(ProgressMapping.ToSummary)], page.Page, page.PageSize, total);
    }

    public async Task<ProgressSubmissionPage> ListSubmissionsAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await ReachesAsync(callerId, projectId, cancellationToken).ConfigureAwait(false))
        {
            return new ProgressSubmissionPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ProgressSubmission> items, int total) = await repository.PageSubmissionsAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        return new ProgressSubmissionPage([.. items.Select(ProgressMapping.ToDetail)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> GetSubmissionAsync(Guid callerId, Guid submissionId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ProgressView, submissionId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : Versioned(loaded.Submission!);
    }

    public async Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> StartAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken)
    {
        Reached reached = await ReachAsync(callerId, PermissionCatalogue.ProgressSubmit, projectId, cancellationToken).ConfigureAwait(false);
        if (reached.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = reached.Project!;
        if (project.Status != ProjectLifecycleState.Active)
        {
            return AdministrationError.Rule(ProgressErrorCodes.ProjectNotActive);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = ReportingCalendar.Today(now);
        IReadOnlyList<ReportingCycle> cycles = await GenerateDueCyclesAsync(callerId, project, now, cancellationToken).ConfigureAwait(false);

        // One period at a time, earliest first: publication follows the periods' order, so the latest snapshot is the latest period.
        ReportingCycle? cycle = cycles.Where(c => c.Status == ReportingCycleStatus.Open && c.PeriodStart <= today).MinBy(c => c.PeriodStart);
        if (cycle is null)
        {
            return AdministrationError.Conflict(ProgressErrorCodes.NothingToReport);
        }

        IReadOnlyList<ProgressSubmission> revisions = await repository.ListRevisionsAsync(cycle.Id, cancellationToken).ConfigureAwait(false);
        if (revisions.Any(r => r.Status != ProgressSubmissionStatus.Returned))
        {
            return AdministrationError.Conflict(ProgressErrorCodes.SubmissionExists);
        }

        ProgressInputs read = await inputs.ReadAsync(project.Id, cancellationToken).ConfigureAwait(false);
        if (ProgressRollup.Actual(read.WorkBreakdown) is not { } actual)
        {
            return AdministrationError.Rule(ProgressErrorCodes.RollupUnavailable);
        }

        // ADR-017: the last published period's narrative and override are carried forward, so confirming them is the default action.
        ProgressSubmission? previous = await repository.FindLatestPublishedSubmissionAsync(project.Id, cancellationToken).ConfigureAwait(false);
        ProgressSubmission submission = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ReportingCycleId = cycle.Id,
            RevisionNo = revisions.Count == 0 ? 1 : revisions.Max(r => r.RevisionNo) + 1,
            Status = ProgressSubmissionStatus.Draft,
            ActualPercentCalculated = actual,
            ActualPercentOverride = previous?.ActualPercentOverride,
            OverrideReason = previous?.OverrideReason,
            Narrative = previous?.Narrative,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        Plan(submission, read.Baseline, cycle, today);
        repository.Add(submission);
        audit.Stage(ProgressAudit.Started(callerId, project, submission));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            ProgressSaveOutcome.Saved => Versioned(submission),
            ProgressSaveOutcome.Duplicate => AdministrationError.Conflict(ProgressErrorCodes.SubmissionExists),
            ProgressSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> UpdateAsync(
        Guid callerId, Guid submissionId, ProgressSubmissionChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ProgressSubmit, submissionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProgressSubmission submission = loaded.Submission!;
        if (submission.Status != ProgressSubmissionStatus.Draft)
        {
            return AdministrationError.Conflict(ProgressErrorCodes.NotEditable);
        }

        if (changes.Override is { ActualPercent: < 0 or > 100 })
        {
            throw new ArgumentOutOfRangeException(nameof(changes), changes.Override.ActualPercent, "An override is a percentage, 0–100.");
        }

        (NarrativeText? narrative, decimal? overridePercent, NarrativeText? reason) = (submission.Narrative, submission.ActualPercentOverride, submission.OverrideReason);
        submission.Narrative = changes.Narrative;
        submission.ActualPercentOverride = changes.Override?.ActualPercent;
        submission.OverrideReason = changes.Override?.Reason;
        Touch(submission, callerId);
        audit.Stage(ProgressAudit.Changed(callerId, loaded.Project!, submission, narrative, overridePercent, reason));
        return await SaveAsync(submission, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> SubmitAsync(Guid callerId, Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ProgressSubmit, submissionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ProgressSubmission submission) = (loaded.Project!, loaded.Submission!);
        if (!ProgressWorkflow.Allows(submission.Status, ProgressSubmissionStatus.Submitted))
        {
            return AdministrationError.InvalidTransition;
        }

        if (project.Status != ProjectLifecycleState.Active && submission.ProjectIntakeId is null)
        {
            return AdministrationError.Rule(ProgressErrorCodes.ProjectNotActive);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProgressInputs read = await inputs.ReadAsync(project.Id, cancellationToken).ConfigureAwait(false);

        // The figures are fixed at submission (ERD). The opening position keeps the figure entered once at intake (ADR-014).
        if (submission.ProjectIntakeId is null)
        {
            if (ProgressRollup.Actual(read.WorkBreakdown) is not { } actual)
            {
                return AdministrationError.Rule(ProgressErrorCodes.RollupUnavailable);
            }

            ReportingCycle cycle = await repository.FindCycleAsync(submission.ReportingCycleId, cancellationToken).ConfigureAwait(false)
                                   ?? throw new InvalidOperationException($"Submission {submission.Id} has no reporting cycle.");
            submission.ActualPercentCalculated = actual;
            Plan(submission, read.Baseline, cycle, ReportingCalendar.Today(now));
        }

        submission.Status = ProgressSubmissionStatus.Submitted;
        submission.SubmittedByUserId = callerId;
        submission.SubmittedAt = now;
        Touch(submission, callerId);
        audit.Stage(ProgressAudit.Submitted(callerId, project, submission));
        await RecomputeCurrentHealthAsync(callerId, project, read, await policy.HealthRuleAsync(now, cancellationToken).ConfigureAwait(false), now, cancellationToken)
            .ConfigureAwait(false);
        return await SaveAsync(submission, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> StartReviewAsync(Guid callerId, Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadForReviewAsync(callerId, submissionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProgressSubmission submission = loaded.Submission!;
        if (!ProgressWorkflow.Allows(submission.Status, ProgressSubmissionStatus.UnderReview))
        {
            return AdministrationError.InvalidTransition;
        }

        submission.Status = ProgressSubmissionStatus.UnderReview;
        Touch(submission, callerId);
        audit.Stage(ProgressAudit.ReviewStarted(callerId, loaded.Project!, submission));
        return await SaveAsync(submission, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> ReturnAsync(
        Guid callerId, Guid submissionId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        Loaded loaded = await LoadForReviewAsync(callerId, submissionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ProgressSubmission submission) = (loaded.Project!, loaded.Submission!);
        if (!ProgressWorkflow.Allows(submission.Status, ProgressSubmissionStatus.Returned))
        {
            return AdministrationError.InvalidTransition;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        submission.Status = ProgressSubmissionStatus.Returned;
        submission.ReviewedByUserId = callerId;
        submission.ReviewedAt = now;
        submission.ReturnReason = reason;
        Touch(submission, callerId);

        // The period continues as revision + 1, a DRAFT with this revision's content; its figures are derived again on submission.
        ProgressSubmission next = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = submission.ProjectId,
            ReportingCycleId = submission.ReportingCycleId,
            RevisionNo = submission.RevisionNo + 1,
            Status = ProgressSubmissionStatus.Draft,
            ActualPercentCalculated = submission.ActualPercentCalculated,
            ActualPercentOverride = submission.ActualPercentOverride,
            OverrideReason = submission.OverrideReason,
            PlannedPercent = submission.PlannedPercent,
            BaselineId = submission.BaselineId,
            Narrative = submission.Narrative,
            ProjectIntakeId = submission.ProjectIntakeId,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(next);
        audit.Stage(ProgressAudit.Returned(callerId, project, submission));
        audit.Stage(ProgressAudit.Started(callerId, project, next));
        return await SaveAsync(submission, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> PublishAsync(Guid callerId, Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadForReviewAsync(callerId, submissionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ProgressSubmission submission) = (loaded.Project!, loaded.Submission!);
        ReportingCycle cycle = await repository.FindCycleAsync(submission.ReportingCycleId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Submission {submission.Id} has no reporting cycle.");
        if (!ProgressWorkflow.Allows(submission.Status, ProgressSubmissionStatus.Published) || cycle.Status != ReportingCycleStatus.Open)
        {
            return AdministrationError.InvalidTransition;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        HealthRule rule = await policy.HealthRuleAsync(now, cancellationToken).ConfigureAwait(false);
        ProgressInputs read = await inputs.ReadAsync(project.Id, cancellationToken).ConfigureAwait(false);

        // ICD-03: the official health is computed here, from the figures being published, under the rule in force, and pinned.
        PublishedProgressSnapshot snapshot = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ReportingCycleId = cycle.Id,
            ProgressSubmissionId = submission.Id,
            PublishedAt = now,
            PublishedByUserId = callerId,
            ActualPercent = submission.EffectiveActualPercent,
            IsOverridden = submission.IsOverridden,
            PlannedPercent = submission.PlannedPercent,
            OverallHealth = OverallHealthRule.Compute(
                submission.EffectiveActualPercent, submission.PlannedPercent, read.ScheduleHealth, read.FinancialStatus, rule.Thresholds),
            ScheduleHealth = read.ScheduleHealth,
            FinancialStatus = read.FinancialStatus,
            HealthRuleConfigurationVersionId = rule.ConfigurationVersionId,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(snapshot);

        submission.Status = ProgressSubmissionStatus.Published;
        submission.ReviewedByUserId = callerId;
        submission.ReviewedAt = now;
        Touch(submission, callerId);
        cycle.Status = ReportingCycleStatus.Closed;
        cycle.UpdatedAt = now;
        cycle.UpdatedBy = callerId;
        audit.Stage(ProgressAudit.Published(callerId, project, submission, snapshot));
        await RecomputeCurrentHealthAsync(callerId, project, read, rule, now, cancellationToken).ConfigureAwait(false);
        return await SaveAsync(submission, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PublishedProgressSnapshotPage> ListSnapshotsAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await ReachesAsync(callerId, projectId, cancellationToken).ConfigureAwait(false))
        {
            return new PublishedProgressSnapshotPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<PublishedProgressSnapshot> items, int total) = await repository.PageSnapshotsAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        return new PublishedProgressSnapshotPage([.. items.Select(ProgressMapping.ToDetail)], page.Page, page.PageSize, total);
    }

    public async Task<ProjectHealthStatusPage> ListHealthStatusesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ProjectHealthStatus? health = await ReachesAsync(callerId, projectId, cancellationToken).ConfigureAwait(false)
            ? await repository.FindHealthStatusAsync(projectId, track: false, cancellationToken).ConfigureAwait(false)
            : null;
        return health is not null && page.Page == 1
            ? new ProjectHealthStatusPage([ProgressMapping.ToDetail(health)], page.Page, page.PageSize, 1)
            : new ProjectHealthStatusPage([], page.Page, page.PageSize, health is null ? 0 : 1);
    }

    /// <summary>
    /// The periods due up to today, generated from the governance profile's cadence and staged with this unit of work; with
    /// those already generated, earliest first. The cadence is resolved only when a period is due.
    /// </summary>
    private async Task<IReadOnlyList<ReportingCycle>> GenerateDueCyclesAsync(Guid callerId, ProjectFacts project, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<ReportingCycle> cycles = [.. await repository.ListCyclesAsync(project.Id, cancellationToken).ConfigureAwait(false)];
        if (ReportingCalendar.FirstPeriodStart(project) is not { } first)
        {
            return cycles;
        }

        DateOnly today = ReportingCalendar.Today(now);
        DateOnly next = ReportingCalendar.NextPeriodStart(first, cycles.Count == 0 ? null : cycles.Max(c => c.PeriodEnd));
        if (next > today)
        {
            return cycles;
        }

        int cadenceDays = await policy.UpdateCadenceDaysAsync(project.GovernanceProfileItemId, now, cancellationToken).ConfigureAwait(false);
        foreach ((DateOnly start, DateOnly end) in ReportingCalendar.PeriodsBegunBy(next, cadenceDays, today))
        {
            ReportingCycle cycle = new()
            {
                Id = Guid.CreateVersion7(now),
                ProjectId = project.Id,
                PeriodStart = start,
                PeriodEnd = end,
                DueDate = end,
                Status = ReportingCycleStatus.Open,
                CreatedAt = now,
                CreatedBy = callerId,
                UpdatedAt = now,
                UpdatedBy = callerId,
            };
            repository.Add(cycle);
            audit.Stage(ProgressAudit.CycleCreated(callerId, project, cycle));
            cycles.Add(cycle);
        }

        return cycles;
    }

    /// <summary>
    /// Rewrites the CURRENT/LIVE health from the derived figures as they stand now — never from an unpublished override —
    /// staged with this unit of work (ICD-03, M-12).
    /// </summary>
    private async Task RecomputeCurrentHealthAsync(Guid callerId, ProjectFacts project, ProgressInputs read, HealthRule rule, DateTimeOffset now, CancellationToken cancellationToken)
    {
        decimal? actual = ProgressRollup.Actual(read.WorkBreakdown);
        decimal? planned = ProgressRollup.Planned(read.Baseline, ReportingCalendar.Today(now));
        ProjectHealthStatus? health = await repository.FindHealthStatusAsync(project.Id, track: true, cancellationToken).ConfigureAwait(false);
        HealthStatus? before = health?.OverallHealth;
        if (health is null)
        {
            health = new ProjectHealthStatus { Id = Guid.CreateVersion7(now), ProjectId = project.Id, CreatedAt = now, CreatedBy = callerId };
            repository.Add(health);
        }

        health.OverallHealth = OverallHealthRule.Compute(actual, planned, read.ScheduleHealth, read.FinancialStatus, rule.Thresholds);
        health.ActualPercent = actual;
        health.PlannedPercent = planned;
        health.ComputedAt = now;
        health.HealthRuleConfigurationVersionId = rule.ConfigurationVersionId;
        health.UpdatedAt = now;
        health.UpdatedBy = callerId;
        audit.Stage(ProgressAudit.HealthRecomputed(callerId, project, health, before));
    }

    /// <summary>Planned progress at the period's end, or today while the period runs (ADR-009: from the baseline, never entered).</summary>
    private static void Plan(ProgressSubmission submission, BaselinePlan? baseline, ReportingCycle cycle, DateOnly today)
    {
        submission.PlannedPercent = ProgressRollup.Planned(baseline, today < cycle.PeriodEnd ? today : cycle.PeriodEnd);
        submission.BaselineId = submission.PlannedPercent is null ? null : baseline!.BaselineId;
    }

    /// <summary>Whether the caller may view the project's progress: a collection of a project they may not see is empty.</summary>
    private async Task<bool> ReachesAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false);

    private async Task<Reached> ReachAsync(Guid callerId, string permissionCode, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectFacts? project = await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        return project is null ? new Reached(null, AdministrationError.NotFound)
            : new Reached(project, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Loaded> LoadAsync(Guid callerId, string permissionCode, Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProgressSubmission? submission = await repository.FindSubmissionAsync(submissionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return new Loaded(null, null, AdministrationError.NotFound);
        }

        Reached reached = await ReachAsync(callerId, permissionCode, submission.ProjectId, cancellationToken).ConfigureAwait(false);
        return new Loaded(reached.Project, submission, reached.Error);
    }

    private async Task<Loaded> LoadForReviewAsync(Guid callerId, Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProgressSubmission? submission = await repository.FindSubmissionAsync(submissionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = submission is null ? null : await projects.FindAsync(submission.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded(null, null, AdministrationError.NotFound)
            : new Loaded(project, submission, await access.CheckReviewAsync(callerId, project, submission!, cancellationToken).ConfigureAwait(false));
    }

    private async Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> SaveAsync(ProgressSubmission submission, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ProgressSaveOutcome.Saved
            ? Versioned(submission)
            : AdministrationError.PreconditionFailed;

    private Versioned<ProgressSubmissionDetail> Versioned(ProgressSubmission submission) => new(ProgressMapping.ToDetail(submission), repository.RowVersionOf(submission));

    private void Touch(ProgressSubmission submission, Guid by)
    {
        submission.UpdatedAt = timeProvider.GetUtcNow();
        submission.UpdatedBy = by;
    }

    private sealed record Reached(ProjectFacts? Project, AdministrationError? Error);

    private sealed record Loaded(ProjectFacts? Project, ProgressSubmission? Submission, AdministrationError? Error);
}
