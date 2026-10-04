using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;
using static PMPlatform.Application.Features.FinancialKpi.FinancialKpiMasking;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// Financial Progress (TASK-052). A period's update — the periods are WF-02's, read through edge 13 — carries the actual
/// expenditure to date and the forecast at completion, each null and explained by its value status when not known. AHDA reviews
/// and publishes it into a <see cref="PublishedFinancialSnapshot"/> that copies every figure, the Approved Budget in force and the
/// status rated under pinned thresholds, and is never touched again. The CURRENT/LIVE position is computed on read and stored
/// nowhere, so the two views cannot merge (M-12).
/// </summary>
internal sealed class FinancialProgressService(
    IFinancialKpiRepository repository,
    IProjectFactsReader projects,
    IReportingCycleReader cycles,
    FinancialKpiAccess access,
    FinancialPolicy policy,
    IAuditTrail audit,
    TimeProvider timeProvider) : IFinancialProgressService
{
    public async Task<FinancialProgressUpdatePage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await CanViewAsync(callerId, projectId, cancellationToken).ConfigureAwait(false))
        {
            return new FinancialProgressUpdatePage([], page.Page, page.PageSize, 0);
        }

        FieldMask mask = await MaskAsync(callerId, Update, cancellationToken).ConfigureAwait(false);
        (IReadOnlyList<FinancialProgressUpdate> items, int total) = await repository.PageUpdatesAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        return new FinancialProgressUpdatePage([.. items.Select(u => FinancialKpiMapping.ToDetail(u, mask))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> GetAsync(Guid callerId, Guid updateId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialView, updateId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(callerId, loaded.Update!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> StartAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectFacts? project = await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if ((project is null ? AdministrationError.NotFound
                : await access.CheckAsync(callerId, PermissionCatalogue.FinancialSubmit, project, cancellationToken).ConfigureAwait(false)
                  ?? ProjectEligibility.ReportingRefused(project)) is { } refused)
        {
            return refused;
        }

        // One period at a time, earliest first, as WF-02 reports progress: the latest snapshot is then the latest period.
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = ValueRules.Today(now);
        IReadOnlyList<FinancialProgressUpdate> updates = await repository.ListUpdatesAsync(project!.Id, cancellationToken).ConfigureAwait(false);
        HashSet<Guid> published = [.. updates.Where(u => u.Status == FinancialUpdateStatus.Published).Select(u => u.ReportingCycleId)];
        ReportingCycleSummary? cycle = (await cycles.ListAsync(project.Id, cancellationToken).ConfigureAwait(false))
            .Where(c => c.PeriodStart <= today && !published.Contains(c.Id))
            .MinBy(c => c.PeriodStart);
        if (cycle is null)
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.NothingToReport);
        }

        List<FinancialProgressUpdate> revisions = [.. updates.Where(u => u.ReportingCycleId == cycle.Id)];
        if (revisions.Any(r => r.Status != FinancialUpdateStatus.Returned))
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.UpdateExists);
        }

        // Nothing is presumed: every figure starts Unknown, MISSING, until someone enters it.
        FinancialProgressUpdate update = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ReportingCycleId = cycle.Id,
            RevisionNo = revisions.Count == 0 ? 1 : revisions.Max(r => r.RevisionNo) + 1,
            Status = FinancialUpdateStatus.Draft,
            ValueStatus = ValueStatus.Missing,
            SourceType = FinancialSourceType.Manual,
            AsOfDate = today,
            EnteredByUserId = callerId,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(update);
        audit.Stage(FinancialKpiAudit.UpdateStarted(callerId, project, update));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            FinancialKpiSaveOutcome.Saved => await VersionedAsync(callerId, update, cancellationToken).ConfigureAwait(false),
            FinancialKpiSaveOutcome.Duplicate => AdministrationError.Conflict(FinancialKpiErrorCodes.UpdateExists),
            FinancialKpiSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> UpdateAsync(
        Guid callerId, Guid updateId, FinancialProgressUpdateChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialSubmit, updateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, FinancialProgressUpdate update) = (loaded.Project!, loaded.Update!);
        if (update.Status != FinancialUpdateStatus.Draft)
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.NotEditable);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await FiguresRefusedAsync(project, changes.ActualExpenditureToDateSar, changes.ForecastAtCompletionSar, changes.ValueStatus, changes.AsOfDate, now, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        UpdateFigures before = UpdateFigures.Of(update);
        update.ActualExpenditureToDateSar = changes.ActualExpenditureToDateSar;
        update.ForecastAtCompletionSar = changes.ForecastAtCompletionSar;
        update.ValueStatus = changes.ValueStatus;
        update.Narrative = changes.Narrative;
        update.SourceReference = changes.SourceReference;
        update.AsOfDate = changes.AsOfDate;
        update.EnteredByUserId = callerId;
        Touch(update, callerId, now);
        audit.Stage(FinancialKpiAudit.UpdateChanged(callerId, project, before, update));
        return await SaveAsync(callerId, update, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialSubmit, updateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        if (loaded.Update!.Status != FinancialUpdateStatus.Draft)
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.NotEditable);
        }

        audit.Stage(FinancialKpiAudit.UpdateDeleted(callerId, loaded.Project!, loaded.Update));
        repository.Remove(loaded.Update);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == FinancialKpiSaveOutcome.Saved ? null : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> SubmitAsync(Guid callerId, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialSubmit, updateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, FinancialProgressUpdate update) = (loaded.Project!, loaded.Update!);
        if (!FinancialUpdateWorkflow.Allows(update.Status, FinancialUpdateStatus.Submitted))
        {
            return AdministrationError.InvalidTransition;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await FiguresRefusedAsync(project, update.ActualExpenditureToDateSar, update.ForecastAtCompletionSar, update.ValueStatus, update.AsOfDate, now, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        update.Status = FinancialUpdateStatus.Submitted;
        update.SubmittedByUserId = callerId;
        update.SubmittedAt = now;
        Touch(update, callerId, now);
        audit.Stage(FinancialKpiAudit.UpdateTransition(FinancialKpiAuditEvents.UpdateSubmitted, callerId, project, update, FinancialUpdateStatus.Draft));
        return await SaveAsync(callerId, update, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> StartReviewAsync(Guid callerId, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadForReviewAsync(callerId, updateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        FinancialProgressUpdate update = loaded.Update!;
        if (!FinancialUpdateWorkflow.Allows(update.Status, FinancialUpdateStatus.UnderReview))
        {
            return AdministrationError.InvalidTransition;
        }

        update.Status = FinancialUpdateStatus.UnderReview;
        Touch(update, callerId, timeProvider.GetUtcNow());
        audit.Stage(FinancialKpiAudit.UpdateTransition(FinancialKpiAuditEvents.UpdateReviewStarted, callerId, loaded.Project!, update, FinancialUpdateStatus.Submitted));
        return await SaveAsync(callerId, update, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> ReturnAsync(
        Guid callerId, Guid updateId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Loaded loaded = await LoadForReviewAsync(callerId, updateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, FinancialProgressUpdate update) = (loaded.Project!, loaded.Update!);
        if (!FinancialUpdateWorkflow.Allows(update.Status, FinancialUpdateStatus.Returned))
        {
            return AdministrationError.InvalidTransition;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        update.Status = FinancialUpdateStatus.Returned;
        update.ReviewedByUserId = callerId;
        update.ReviewedAt = now;
        update.ReturnReason = reason;
        Touch(update, callerId, now);

        // The period continues as revision + 1, a DRAFT with this revision's figures and provenance, for its author to correct.
        FinancialProgressUpdate next = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = update.ProjectId,
            ReportingCycleId = update.ReportingCycleId,
            RevisionNo = update.RevisionNo + 1,
            Status = FinancialUpdateStatus.Draft,
            ActualExpenditureToDateSar = update.ActualExpenditureToDateSar,
            ForecastAtCompletionSar = update.ForecastAtCompletionSar,
            ValueStatus = update.ValueStatus,
            Narrative = update.Narrative,
            ProjectIntakeId = update.ProjectIntakeId,
            SourceType = update.SourceType,
            SourceReference = update.SourceReference,
            AsOfDate = update.AsOfDate,
            EnteredByUserId = update.EnteredByUserId,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(next);
        audit.Stage(FinancialKpiAudit.UpdateTransition(FinancialKpiAuditEvents.UpdateReturned, callerId, project, update, FinancialUpdateStatus.UnderReview));
        audit.Stage(FinancialKpiAudit.UpdateStarted(callerId, project, next));
        return await SaveAsync(callerId, update, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> PublishAsync(Guid callerId, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadForReviewAsync(callerId, updateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, FinancialProgressUpdate update) = (loaded.Project!, loaded.Update!);
        if (!FinancialUpdateWorkflow.Allows(update.Status, FinancialUpdateStatus.Published))
        {
            return AdministrationError.InvalidTransition;
        }

        // The official status is rated here, from the figures being published and the budget in force, under the thresholds in
        // force, and pinned with them. Without thresholds nothing is published: 422 CONFIGURATION_MISSING.
        DateTimeOffset now = timeProvider.GetUtcNow();
        FinancialStatusRuleVersion rule = await policy.ThresholdsAsync(now, cancellationToken).ConfigureAwait(false);
        FinancialCommitment? budget = await repository.FindActiveCommitmentAsync(project.Id, CommitmentType.ApprovedBudget, track: false, cancellationToken).ConfigureAwait(false);
        PublishedFinancialSnapshot snapshot = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ReportingCycleId = update.ReportingCycleId,
            FinancialProgressUpdateId = update.Id,
            FinancialCommitmentId = budget?.Id,
            PublishedAt = now,
            PublishedByUserId = callerId,
            ApprovedBudgetSar = budget?.AmountSar,
            ActualExpenditureToDateSar = update.ActualExpenditureToDateSar,
            ForecastAtCompletionSar = update.ForecastAtCompletionSar,
            ValueStatus = update.ValueStatus,
            FinancialStatus = FinancialStatusRule.Rate(budget?.AmountSar, update.ForecastAtCompletionSar, update.ValueStatus, rule.Thresholds),
            ThresholdConfigurationVersionId = rule.ConfigurationVersionId,
            SourceType = update.SourceType,
            SourceReference = update.SourceReference,
            AsOfDate = update.AsOfDate,
            EnteredByUserId = update.EnteredByUserId,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(snapshot);

        update.Status = FinancialUpdateStatus.Published;
        update.ReviewedByUserId = callerId;
        update.ReviewedAt = now;
        Touch(update, callerId, now);
        audit.Stage(FinancialKpiAudit.Published(callerId, project, update, snapshot));
        return await SaveAsync(callerId, update, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PublishedFinancialSnapshotPage> ListSnapshotsAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await CanViewAsync(callerId, projectId, cancellationToken).ConfigureAwait(false))
        {
            return new PublishedFinancialSnapshotPage([], page.Page, page.PageSize, 0);
        }

        FieldMask mask = await MaskAsync(callerId, Snapshot, cancellationToken).ConfigureAwait(false);
        (IReadOnlyList<PublishedFinancialSnapshot> items, int total) = await repository.PageSnapshotsAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        return new PublishedFinancialSnapshotPage([.. items.Select(s => FinancialKpiMapping.ToDetail(s, mask))], page.Page, page.PageSize, total);
    }

    public async Task<FinancialPositionPage> ListPositionsAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await CanViewAsync(callerId, projectId, cancellationToken).ConfigureAwait(false))
        {
            return new FinancialPositionPage([], page.Page, page.PageSize, 0);
        }

        PositionMasks masks = await MasksAsync(callerId, cancellationToken).ConfigureAwait(false);
        FinancialPositionDetail position = (await PositionsAsync([projectId], masks, cancellationToken).ConfigureAwait(false))[0];
        return new FinancialPositionPage(page.Page == 1 ? [position] : [], page.Page, page.PageSize, 1);
    }

    public async Task<FinancialPortfolioAggregate> AggregateAsync(Guid callerId, IReadOnlyList<Guid> projectIds, SemanticState semanticState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        List<Guid> visible = [];
        List<AggregateExclusion> unavailable = [];
        foreach (Guid projectId in projectIds.Distinct())
        {
            if (await CanViewAsync(callerId, projectId, cancellationToken).ConfigureAwait(false))
            {
                visible.Add(projectId);
            }
            else
            {
                unavailable.Add(new AggregateExclusion(projectId, null, AggregateExclusionReason.NotAvailable));
            }
        }

        PositionMasks masks = await MasksAsync(callerId, cancellationToken).ConfigureAwait(false);
        List<FinancialFigures> figures = [];
        if (semanticState == SemanticState.PublishedOfficial)
        {
            bool masked = !(masks.Snapshot.Reveals(ApprovedBudgetSar) && masks.Snapshot.Reveals(ActualExpenditureToDateSar) && masks.Snapshot.Reveals(ForecastAtCompletionSar));
            IReadOnlyList<PublishedFinancialSnapshot> latest = await repository.ListLatestSnapshotsAsync(visible, cancellationToken).ConfigureAwait(false);
            unavailable.AddRange(visible.Where(id => latest.All(s => s.ProjectId != id)).Select(id => new AggregateExclusion(id, null, AggregateExclusionReason.NoPublishedFigure)));
            figures.AddRange(latest.Select(s => new FinancialFigures(
                s.ProjectId, Money.CurrencyCode, s.ApprovedBudgetSar, s.ActualExpenditureToDateSar, s.ForecastAtCompletionSar, s.ValueStatus, masked)));
        }
        else
        {
            bool masked = !(masks.Commitment.Reveals(AmountSar) && masks.Update.Reveals(ActualExpenditureToDateSar) && masks.Update.Reveals(ForecastAtCompletionSar));
            figures.AddRange((await PositionsAsync(visible, masks, cancellationToken).ConfigureAwait(false)).Select(p => new FinancialFigures(
                p.ProjectId, Money.CurrencyCode, p.ApprovedBudgetSar, p.ActualExpenditureToDateSar, p.ForecastAtCompletionSar, p.ValueStatus, masked)));
        }

        return FinancialAggregation.Aggregate(semanticState, projectIds.Distinct().Count(), figures, unavailable);
    }

    /// <summary>
    /// The CURRENT/LIVE position of each project, in the order given: the ACTIVE Approved Budget beside the figures of the latest
    /// revision submitted for review or published, rated under the thresholds in force — UNKNOWN when there are none.
    /// </summary>
    private async Task<IReadOnlyList<FinancialPositionDetail>> PositionsAsync(IReadOnlyList<Guid> projectIds, PositionMasks masks, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        FinancialStatusRuleVersion? rule = await policy.FindThresholdsAsync(now, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FinancialCommitment> budgets = await repository.ListActiveBudgetsAsync(projectIds, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FinancialProgressUpdate> reported = await repository.ListLatestReportedUpdatesAsync(projectIds, cancellationToken).ConfigureAwait(false);
        return [.. projectIds.Select(projectId =>
        {
            FinancialCommitment? budget = budgets.SingleOrDefault(b => b.ProjectId == projectId);
            FinancialProgressUpdate? update = reported.SingleOrDefault(u => u.ProjectId == projectId);
            ValueStatus valueStatus = update?.ValueStatus ?? ValueStatus.Missing;
            FinancialStatus status = rule is null ? FinancialStatus.Unknown : FinancialStatusRule.Rate(budget?.AmountSar, update?.ForecastAtCompletionSar, valueStatus, rule.Thresholds);
            return new FinancialPositionDetail(
                projectId,
                SemanticState.CurrentLive,
                budget?.Id,
                Apply(masks.Commitment, AmountSar, budget?.AmountSar),
                update?.Id,
                update?.Status,
                Apply(masks.Update, ActualExpenditureToDateSar, update?.ActualExpenditureToDateSar),
                Apply(masks.Update, ForecastAtCompletionSar, update?.ForecastAtCompletionSar),
                valueStatus,
                status,
                rule?.ConfigurationVersionId,
                update?.AsOfDate,
                now,
                [.. masks.Commitment.MaskedFields.Contains(AmountSar) ? [ApprovedBudgetSar] : Array.Empty<string>(),
                    .. masks.Update.MaskedFields.Where(f => f is ActualExpenditureToDateSar or ForecastAtCompletionSar)]);
        })];
    }

    /// <summary>The project admits periodic figures; the figures obey the value-status rule; none is entered for an INTEGRATED field.</summary>
    private async Task<AdministrationError?> FiguresRefusedAsync(
        ProjectFacts project, Money? actual, Money? forecast, ValueStatus valueStatus, DateOnly asOfDate, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if ((ProjectEligibility.ReportingRefused(project) ?? ValueRules.FinancialRefused(actual, forecast, valueStatus, asOfDate, ValueRules.Today(now))) is { } refused)
        {
            return refused;
        }

        if (actual is { Amount: < 0 } || forecast is { Amount: < 0 })
        {
            return AdministrationError.Rule(FinancialKpiErrorCodes.ValueStatusInvalid, new FieldIssue(actual is { Amount: < 0 } ? "actualExpenditureToDateSar" : "forecastAtCompletionSar", FieldIssue.NotAllowed));
        }

        List<(FinancialField, string)> entered = [];
        if (actual is not null)
        {
            entered.Add((FinancialField.ActualExpenditure, "actualExpenditureToDateSar"));
        }

        if (forecast is not null)
        {
            entered.Add((FinancialField.ForecastAtCompletion, "forecastAtCompletionSar"));
        }

        return await FinancialSourceModeService.ManualEntryRefusedAsync(repository, project.Id, entered, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> CanViewAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanAsync(callerId, PermissionCatalogue.FinancialView, project, cancellationToken).ConfigureAwait(false);

    private Task<FieldMask> MaskAsync(Guid callerId, string entityCode, CancellationToken cancellationToken) =>
        access.MaskAsync(callerId, PermissionCatalogue.FinancialView, entityCode, cancellationToken);

    private async Task<PositionMasks> MasksAsync(Guid callerId, CancellationToken cancellationToken) => new(
        await MaskAsync(callerId, Commitment, cancellationToken).ConfigureAwait(false),
        await MaskAsync(callerId, Update, cancellationToken).ConfigureAwait(false),
        await MaskAsync(callerId, Snapshot, cancellationToken).ConfigureAwait(false));

    private async Task<Loaded> LoadAsync(Guid callerId, string permissionCode, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        FinancialProgressUpdate? update = await repository.FindUpdateAsync(updateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = update is null ? null : await projects.FindAsync(update.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded(null, null, AdministrationError.NotFound)
            : new Loaded(project, update, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Review is AHDA's gate: internal reviewers only, never the revision's submitter (ADR-013).</summary>
    private async Task<Loaded> LoadForReviewAsync(Guid callerId, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        FinancialProgressUpdate? update = await repository.FindUpdateAsync(updateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = update is null ? null : await projects.FindAsync(update.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded(null, null, AdministrationError.NotFound)
            : new Loaded(project, update, await access.CheckGateAsync(
                callerId, PermissionCatalogue.FinancialReview, project, update!.SubmittedByUserId,
                reason => FinancialKpiAudit.ReviewRefused(callerId, project, FinancialKpiAudit.UpdateType, update.Id, reason), cancellationToken).ConfigureAwait(false));
    }

    private async Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> SaveAsync(Guid callerId, FinancialProgressUpdate update, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            FinancialKpiSaveOutcome.Saved => await VersionedAsync(callerId, update, cancellationToken).ConfigureAwait(false),
            FinancialKpiSaveOutcome.Duplicate => AdministrationError.InvalidTransition,
            FinancialKpiSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };

    private async Task<Versioned<FinancialProgressUpdateDetail>> VersionedAsync(Guid callerId, FinancialProgressUpdate update, CancellationToken cancellationToken) =>
        new(FinancialKpiMapping.ToDetail(update, await MaskAsync(callerId, Update, cancellationToken).ConfigureAwait(false)), repository.RowVersionOf(update));

    private static void Touch(FinancialProgressUpdate update, Guid by, DateTimeOffset at)
    {
        update.UpdatedAt = at;
        update.UpdatedBy = by;
    }

    private sealed record Loaded(ProjectFacts? Project, FinancialProgressUpdate? Update, AdministrationError? Error);

    private sealed record PositionMasks(FieldMask Commitment, FieldMask Update, FieldMask Snapshot);
}
