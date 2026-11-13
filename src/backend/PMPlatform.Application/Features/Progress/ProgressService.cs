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
    ReportingPeriods periods,
    IProjectMonthlyUpdates declared,
    ProgressStages stages,
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
        ILookup<Guid, ProgressStageEntry> entries = await repository.ReadStagesAsync([.. items.Select(s => s.Id)], cancellationToken).ConfigureAwait(false);
        List<ProgressSubmissionDetail> details = [];
        foreach (ProgressSubmission item in items)
        {
            details.Add(await DetailAsync(item, [.. entries[item.Id]], cancellationToken).ConfigureAwait(false));
        }

        return new ProgressSubmissionPage(details, page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> GetSubmissionAsync(Guid callerId, Guid submissionId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.ProgressView, submissionId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(loaded.Submission!, cancellationToken).ConfigureAwait(false);
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
        IReadOnlyList<ReportingCycle> cycles = await periods.StageDueAsync(callerId, project, now, cancellationToken).ConfigureAwait(false);

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
        (decimal? actual, ProgressActualSource source) = read.Actual();

        // ADR-017: the last published period's narrative and override are carried forward, so confirming them is the default action.
        ProgressSubmission? previous = await repository.FindLatestPublishedSubmissionAsync(project.Id, cancellationToken).ConfigureAwait(false);
        ProgressSubmission submission = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ReportingCycleId = cycle.Id,
            RevisionNo = revisions.Count == 0 ? 1 : revisions.Max(r => r.RevisionNo) + 1,
            Status = ProgressSubmissionStatus.Draft,
            ActualPercentCalculated = actual ?? 0,
            ActualSource = source,
            ActualPercentOverride = previous?.ActualPercentOverride,
            OverrideReason = previous?.OverrideReason,
            Narrative = previous?.Narrative,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        Plan(submission, read, cycle, today);

        // Review round 7, item 1: with project stages configured, the period is stage-based and the stage plan comes first.
        if (await stages.StartAsync(submission, project, read, AsOf(cycle, today), now, callerId, cancellationToken).ConfigureAwait(false) is { } unplanned)
        {
            return unplanned;
        }

        repository.Add(submission);
        audit.Stage(ProgressAudit.Started(callerId, project, submission));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            ProgressSaveOutcome.Saved => await VersionedAsync(submission, cancellationToken).ConfigureAwait(false),
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

        if (changes.Stages is { } stageChanges
            && await stages.ApplyAsync(submission, stageChanges, callerId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
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
        if (submission.ProjectIntakeId is null && submission.StageConfigurationVersionId is not null)
        {
            // Review round 7, item 1: a stage-based period needs no work breakdown; its figures are its stages'.
            ReportingCycle cycle = await repository.FindCycleAsync(submission.ReportingCycleId, cancellationToken).ConfigureAwait(false)
                                   ?? throw new InvalidOperationException($"Submission {submission.Id} has no reporting cycle.");
            if (await stages.SubmitAsync(submission, read, AsOf(cycle, ReportingCalendar.Today(now)), cancellationToken).ConfigureAwait(false) is { } incomplete)
            {
                return incomplete;
            }
        }
        else if (submission.ProjectIntakeId is null)
        {
            (decimal? actual, ProgressActualSource source) = read.Actual();
            if (actual is null && !submission.IsOverridden)
            {
                return AdministrationError.Rule(ProgressErrorCodes.RollupUnavailable, new FieldIssue("override", FieldIssue.Required));
            }

            ReportingCycle cycle = await repository.FindCycleAsync(submission.ReportingCycleId, cancellationToken).ConfigureAwait(false)
                                   ?? throw new InvalidOperationException($"Submission {submission.Id} has no reporting cycle.");
            submission.ActualPercentCalculated = actual ?? 0;
            submission.ActualSource = source;
            Plan(submission, read, cycle, ReportingCalendar.Today(now));
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
            ActualSource = submission.ActualSource,
            ActualPercentOverride = submission.ActualPercentOverride,
            OverrideReason = submission.OverrideReason,
            PlannedPercent = submission.PlannedPercent,
            BaselineId = submission.BaselineId,
            Narrative = submission.Narrative,
            ProjectIntakeId = submission.ProjectIntakeId,
            StageConfigurationVersionId = submission.StageConfigurationVersionId,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(next);
        await stages.CopyAsync(submission, next, now, callerId, cancellationToken).ConfigureAwait(false);
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

        // A project with no work breakdown has only its declared figure on the register and the dashboards, so the published one replaces it. A
        // stage-based period's figures replace the declared ones everywhere, planned and current stage included (review round 7, item 1).
        if (submission.ActualSource == ProgressActualSource.StageWeighted)
        {
            ProgressStageEntry? current = ProgressStages.Current(await repository.ListStagesAsync(submission.Id, track: false, cancellationToken).ConfigureAwait(false));
            await declared.StagePublishedProgressAsync(
                project.Id,
                new PublishedProgressValues(submission.EffectiveActualPercent, true, submission.PlannedPercent, current?.StageCode, current?.PercentComplete),
                callerId,
                cancellationToken).ConfigureAwait(false);
            await RecomputeCurrentHealthAsync(callerId, project, read, rule, now, cancellationToken, (submission.EffectiveActualPercent, submission.PlannedPercent))
                .ConfigureAwait(false);
        }
        else
        {
            if (submission.ActualSource != ProgressActualSource.WorkBreakdown)
            {
                await declared.StagePublishedProgressAsync(project.Id, new PublishedProgressValues(submission.EffectiveActualPercent), callerId, cancellationToken)
                    .ConfigureAwait(false);
            }

            await RecomputeCurrentHealthAsync(callerId, project, read, rule, now, cancellationToken).ConfigureAwait(false);
        }

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
    /// Rewrites the CURRENT/LIVE health from the derived figures as they stand now — never from an unpublished override —
    /// staged with this unit of work (ICD-03, M-12).
    /// </summary>
    private async Task RecomputeCurrentHealthAsync(
        Guid callerId, ProjectFacts project, ProgressInputs read, HealthRule rule, DateTimeOffset now, CancellationToken cancellationToken,
        (decimal? Actual, decimal? Planned)? published = null)
    {
        decimal? actual = published is { } stageBased ? stageBased.Actual : read.Actual().Percent;
        decimal? planned = published is { } stagePlanned ? stagePlanned.Planned : ProgressRollup.Planned(read.Baseline, ReportingCalendar.Today(now));
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
    private static void Plan(ProgressSubmission submission, ProgressInputs read, ReportingCycle cycle, DateOnly today)
    {
        submission.PlannedPercent = ProgressRollup.Planned(read.Baseline, AsOf(cycle, today));
        submission.BaselineId = submission.PlannedPercent is null ? null : read.Baseline!.BaselineId;
    }

    /// <summary>The date a period's planned figures are taken on: its end, or today while it runs.</summary>
    private static DateOnly AsOf(ReportingCycle cycle, DateOnly today) => today < cycle.PeriodEnd ? today : cycle.PeriodEnd;

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
            ? await VersionedAsync(submission, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;

    private async Task<Versioned<ProgressSubmissionDetail>> VersionedAsync(ProgressSubmission submission, CancellationToken cancellationToken) =>
        new(
            await DetailAsync(submission, await repository.ListStagesAsync(submission.Id, track: false, cancellationToken).ConfigureAwait(false), cancellationToken)
                .ConfigureAwait(false),
            repository.RowVersionOf(submission));

    private async Task<ProgressSubmissionDetail> DetailAsync(ProgressSubmission submission, IReadOnlyList<ProgressStageEntry> entries, CancellationToken cancellationToken) =>
        submission.StageConfigurationVersionId is null
            ? ProgressMapping.ToDetail(submission)
            : ProgressMapping.ToDetail(submission) with
            {
                StageConfigurationVersionId = submission.StageConfigurationVersionId,
                StageVersionNo = await stages.VersionNoAsync(submission.StageConfigurationVersionId.Value, cancellationToken).ConfigureAwait(false),
                CurrentStageCode = ProgressStages.Current(entries)?.StageCode,
                Stages = await stages.DetailAsync(submission, entries, cancellationToken).ConfigureAwait(false),
            };

    private void Touch(ProgressSubmission submission, Guid by)
    {
        submission.UpdatedAt = timeProvider.GetUtcNow();
        submission.UpdatedBy = by;
    }

    private sealed record Reached(ProjectFacts? Project, AdministrationError? Error);

    private sealed record Loaded(ProjectFacts? Project, ProgressSubmission? Submission, AdministrationError? Error);
}
