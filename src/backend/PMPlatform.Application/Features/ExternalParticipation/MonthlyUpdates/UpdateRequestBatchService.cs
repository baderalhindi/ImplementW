using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.ExternalParticipation.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;

/// <summary>A batch and its issued requests, unsaved.</summary>
internal sealed record BuiltBatch(UpdateRequestBatch Batch, IReadOnlyList<ExternalUpdateRequest> Requests);

/// <summary>
/// Sending monthly updates to one, several or all projects in one go (the 5 Nov 2026 review §2.2). All or nothing: a batch with a project that cannot be sent to answers every such
/// project at once, and creates none. One request per project is issued, grouped as one batch. Who may send: <c>UPDATE_REQUEST_SEND</c> over each project. Each request then runs WF-13's
/// respond and review as any other.
/// </summary>
internal sealed class UpdateRequestBatchService(
    IExternalParticipationRepository repository,
    IProjectFactsReader projectFacts,
    IProjectMonthlyUpdates projects,
    IMasterDataResolver masterData,
    IOutbox outbox,
    ExternalParticipationAccess access,
    ExternalParticipationReferences references,
    ExternalParticipationGate gate,
    IAuditTrail audit,
    TimeProvider timeProvider) : IUpdateRequestBatchService
{
    public const int MaxProjects = 1_000;

    public async Task<AdministrationResult<UpdateRequestBatchDetail>> SendAsync(Guid callerId, UpdateRequestBatchDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (draft.Items.Count is 0 or > MaxProjects || draft.Items.Select(i => i.ProjectId).Distinct().Count() != draft.Items.Count)
        {
            return AdministrationError.Rule(ExternalParticipationErrorCodes.BatchInvalid, new FieldIssue("items", FieldIssue.NotAllowed));
        }

        if (ExternalParticipationReferences.DueDateRefused(draft.DueDate, now) is { } dueRefused)
        {
            return dueRefused;
        }

        AdministrationResult<BuiltBatch> built =
            await BuildAsync(callerId, draft, seriesId: null, now, enforceSendGrant: true, cancellationToken).ConfigureAwait(false);
        if (!built.Succeeded)
        {
            return built.Error;
        }

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(built.Value.Batch);
        foreach (ExternalUpdateRequest request in built.Value.Requests)
        {
            await StageSendAsync(request, callerId, now, cancellationToken).ConfigureAwait(false);
        }

        return await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == ExternalParticipationSaveOutcome.Saved
            ? await GetAsync(callerId, built.Value.Batch.Id, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;
    }

    /// <summary>The batch and its issued requests, unsaved, or every project that cannot be sent to.</summary>
    internal async Task<AdministrationResult<BuiltBatch>> BuildAsync(
        Guid actorId, UpdateRequestBatchDraft draft, Guid? seriesId, DateTimeOffset now, bool enforceSendGrant, CancellationToken cancellationToken)
    {
        IReadOnlyList<MasterDataItemReference> types = await masterData.ListPublishedItemsAsync(MasterDataCatalogueCodes.ContributionType, cancellationToken).ConfigureAwait(false);
        if (types.FirstOrDefault(t => t.Code == ContributionSchemas.MonthlyUpdate) is not { } type)
        {
            return AdministrationError.Rule(ExternalParticipationErrorCodes.SchemaInvalid, new FieldIssue("contributionTypeItemId", FieldIssue.NotFound));
        }

        Guid batchId = Guid.CreateVersion7(now);
        DateOnly? nextRun = draft.RepeatMonthly ? NextRunAfter(DateOnly.FromDateTime(now.UtcDateTime), draft.RepeatDay!.Value) : null;
        UpdateRequestBatch batch = new()
        {
            Id = batchId,
            SeriesId = seriesId ?? batchId,
            DueDate = draft.DueDate,
            Instructions = draft.Instructions,
            ReviewerUserId = draft.ReviewerUserId,
            RepeatMonthly = draft.RepeatMonthly,
            RepeatDay = draft.RepeatDay,
            NextRunOn = nextRun,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };

        List<FieldIssue> issues = [];
        List<ExternalUpdateRequest> requests = [];
        for (int i = 0; i < draft.Items.Count; i++)
        {
            UpdateRequestBatchItem item = draft.Items[i];
            string at = $"items[{i}]";
            ProjectFacts? facts = await projectFacts.FindAsync(item.ProjectId, cancellationToken).ConfigureAwait(false);
            ProjectMonthlySnapshot? project = facts is null ? null : await projects.ReadAsync(item.ProjectId, cancellationToken).ConfigureAwait(false);
            if (facts is null || project is null || project.ExternalEntityId is not { } entity)
            {
                issues.Add(new FieldIssue($"{at}.projectId", facts is null ? FieldIssue.NotFound : ExternalParticipationErrorCodes.ProjectHasNoEntity));
                continue;
            }

            if (enforceSendGrant && !await access.CanSendBatchAsync(actorId, facts, entity, cancellationToken).ConfigureAwait(false))
            {
                issues.Add(new FieldIssue($"{at}.projectId", FieldIssue.NotFound));
                continue;
            }

            if (ExternalParticipationReferences.ProjectRefused(facts) is not null)
            {
                issues.Add(new FieldIssue($"{at}.projectId", ExternalParticipationErrorCodes.ProjectNotEligible));
                continue;
            }

            if ((item.ResponsibleUserId ?? project.EntityContactUserId) is not { } responder)
            {
                issues.Add(new FieldIssue($"{at}.responsibleUserId", FieldIssue.Required));
                continue;
            }

            if (!await access.IsEligibleResponderAsync(responder, facts, entity, cancellationToken, allowInternal: true).ConfigureAwait(false))
            {
                issues.Add(new FieldIssue($"{at}.responsibleUserId", FieldIssue.NotAllowed));
                continue;
            }

            if (!await access.IsEligibleReviewerAsync(draft.ReviewerUserId, facts, entity, cancellationToken).ConfigureAwait(false))
            {
                issues.Add(new FieldIssue("reviewerUserId", FieldIssue.NotAllowed));
                continue;
            }

            if (project.EntityContactUserId is null)
            {
                await projects.StageEntityContactAsync(project.Id, responder, actorId, cancellationToken).ConfigureAwait(false);
            }

            AdministrationResult<ResolvedConfiguration> participation = await references.ParticipationAsync(facts, type.Id, now, cancellationToken).ConfigureAwait(false);
            if (!participation.Succeeded)
            {
                return participation.Error;
            }

            requests.Add(new ExternalUpdateRequest
            {
                Id = Guid.CreateVersion7(now),
                UpdateRequestBatchId = batchId,
                ProjectId = project.Id,
                ExternalEntityId = entity,
                Origin = ExternalRequestOrigin.AhdaIssued,
                ContributionTypeItemId = type.Id,
                ContributionSchemaCode = ContributionSchemas.MonthlyUpdate,
                TargetModule = ProjectMonthlyTarget.Module,
                TargetType = ProjectMonthlyTarget.Type,
                TargetId = project.Id,
                Instructions = draft.Instructions,
                ResponsibleUserId = responder,
                ReviewerUserId = draft.ReviewerUserId,
                DueDate = draft.DueDate,
                Status = ExternalUpdateRequestStatus.Issued,
                ParticipationConfigurationVersionId = participation.Value.VersionId,
                IssuedByUserId = actorId,
                IssuedAt = now,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        return issues.Count > 0
            ? AdministrationError.Rule(ExternalParticipationErrorCodes.BatchInvalid, [.. issues])
            : new BuiltBatch(batch, requests);
    }

    /// <summary>Adds an issued request to the unit of work, with its audit event and its notification to the responder.</summary>
    internal async Task StageSendAsync(ExternalUpdateRequest request, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        repository.Add(request);
        audit.Stage(ExternalParticipationAudit.RequestTransition(ExternalParticipationAuditEvents.RequestIssued, actorId, ExternalUpdateRequestStatus.Draft, request));
        ProjectFacts? facts = await projectFacts.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        outbox.Stage(MonthlyUpdateReminded.Of(request, facts!.DepartmentId, request.ResponsibleUserId!.Value, "SENT", actorId, now));
    }

    public async Task<UpdateRequestBatchPage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        RecordScope scope = await access.ViewScopeAsync(callerId, cancellationToken).ConfigureAwait(false);
        (IReadOnlyList<UpdateRequestBatchSummary> items, int total) = await repository.PageBatchesAsync(scope, page, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return new UpdateRequestBatchPage(items, page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<UpdateRequestBatchDetail>> GetAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken)
    {
        if (await repository.FindBatchAsync(batchId, cancellationToken).ConfigureAwait(false) is not { } batch)
        {
            return AdministrationError.NotFound;
        }

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        IReadOnlyList<ExternalUpdateRequest> requests = await repository.ListRequestsOfBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, ExternalContribution> latest = await repository.FindLatestContributionsAsync([.. requests.Select(r => r.Id)], cancellationToken).ConfigureAwait(false);
        List<UpdateRequestBatchRow> rows = [];
        foreach (ExternalUpdateRequest request in requests)
        {
            if (await projectFacts.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false) is not { } facts
                || !await access.CanViewAsync(callerId, ExternalParticipationAccess.SubjectOf(facts, request), cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            ProjectMonthlySnapshot project = (await projects.ReadAsync(request.ProjectId, cancellationToken).ConfigureAwait(false))!;
            rows.Add(new UpdateRequestBatchRow(
                request.Id, request.ProjectId, project.FormalProjectId, project.LegacyReference, project.Title, request.ExternalEntityId, request.ResponsibleUserId,
                StateOf(request, latest.GetValueOrDefault(request.Id)), ExternalParticipationViews.DueConditionOf(request.Status, request.DueDate, today) == ResponseDueCondition.Overdue,
                repository.RowVersionOf(request)));
        }

        if (rows.Count == 0)
        {
            return AdministrationError.NotFound;
        }

        UpdateRequestBatchSummary summary = new(
            batch.Id, batch.SeriesId, batch.DueDate, batch.RepeatMonthly, batch.RepeatDay, batch.NextRunOn, batch.WithdrawnAt, batch.ReviewerUserId, batch.CreatedBy, batch.CreatedAt,
            rows.Count, rows.Select(r => r.ExternalEntityId).Distinct().Count(), rows.Count(r => r.State is BatchRowState.Sent or BatchRowState.Opened or BatchRowState.Draft or BatchRowState.Returned));
        return new UpdateRequestBatchDetail(summary, batch.Instructions, rows);
    }

    public async Task<AdministrationResult<UpdateRequestBatchDetail>> RemindLateAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken)
    {
        AdministrationResult<UpdateRequestBatchDetail> detail = await GetAsync(callerId, batchId, cancellationToken).ConfigureAwait(false);
        if (!detail.Succeeded)
        {
            return detail;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        foreach (UpdateRequestBatchRow row in detail.Value!.Rows.Where(r => r.Overdue))
        {
            LoadedRequest loaded = await gate.LoadRequestAsync(callerId, PermissionCatalogue.UpdateRequestSend, row.RequestId, null, ParticipationActor.Holder, cancellationToken).ConfigureAwait(false);
            if (loaded.Error is not null || loaded.Request!.LastRemindedOn == today || loaded.Request.ResponsibleUserId is not { } responder)
            {
                continue;
            }

            loaded.Request.LastRemindedOn = today;
            outbox.Stage(MonthlyUpdateReminded.Of(loaded.Request, loaded.Project!.DepartmentId, responder, "OVERDUE", callerId, now));
        }

        await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false);
        return await GetAsync(callerId, batchId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<UpdateRequestBatchDetail>> WithdrawAsync(Guid callerId, Guid batchId, NarrativeText reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        AdministrationResult<UpdateRequestBatchDetail> detail = await GetAsync(callerId, batchId, cancellationToken).ConfigureAwait(false);
        if (!detail.Succeeded)
        {
            return detail;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        foreach (UpdateRequestBatchRow row in detail.Value!.Rows)
        {
            LoadedRequest loaded = await gate.LoadRequestAsync(callerId, PermissionCatalogue.UpdateRequestSend, row.RequestId, null, ParticipationActor.Holder, cancellationToken).ConfigureAwait(false);
            if (loaded.Error is not null || loaded.Request!.Status is not (ExternalUpdateRequestStatus.Issued or ExternalUpdateRequestStatus.InProgress))
            {
                continue;
            }

            ExternalUpdateRequestStatus from = loaded.Request.Status;
            loaded.Request.Status = ExternalUpdateRequestStatus.Cancelled;
            loaded.Request.CancelledAt = now;
            loaded.Request.CancellationReason = reason;
            ExternalParticipationGate.Touch(loaded.Request, callerId, now);
            audit.Stage(ExternalParticipationAudit.RequestTransition(ExternalParticipationAuditEvents.RequestCancelled, callerId, from, loaded.Request));
        }

        UpdateRequestBatch batch = (await repository.FindBatchForUpdateAsync(batchId, cancellationToken).ConfigureAwait(false))!;
        batch.WithdrawnAt = now;
        batch.NextRunOn = null;
        ExternalParticipationGate.Touch(batch, callerId, now);
        return await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == ExternalParticipationSaveOutcome.Saved
            ? await GetAsync(callerId, batchId, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;
    }

    /// <summary>The day of the month after <paramref name="from"/>'s; this month's when it has not come yet is never used, so one sending a month.</summary>
    public static DateOnly NextRunAfter(DateOnly from, int day)
    {
        DateOnly next = new DateOnly(from.Year, from.Month, 1).AddMonths(1);
        return new DateOnly(next.Year, next.Month, day);
    }

    private static BatchRowState StateOf(ExternalUpdateRequest request, ExternalContribution? latest) => request.Status switch
    {
        ExternalUpdateRequestStatus.Cancelled => BatchRowState.Withdrawn,
        ExternalUpdateRequestStatus.Closed when latest?.Status == ExternalContributionStatus.Rejected => BatchRowState.Rejected,
        ExternalUpdateRequestStatus.Closed => BatchRowState.Accepted,
        ExternalUpdateRequestStatus.Responded => BatchRowState.Submitted,
        ExternalUpdateRequestStatus.InProgress when latest is { RevisionNo: > 1, Status: ExternalContributionStatus.Draft } => BatchRowState.Returned,
        ExternalUpdateRequestStatus.InProgress => BatchRowState.Draft,
        ExternalUpdateRequestStatus.Issued when request.OpenedAt is not null => BatchRowState.Opened,
        ExternalUpdateRequestStatus.Issued => BatchRowState.Sent,
        ExternalUpdateRequestStatus.Draft => BatchRowState.Sent,
        _ => throw new ArgumentOutOfRangeException(nameof(request), request.Status, "Unknown request status."),
    };
}
