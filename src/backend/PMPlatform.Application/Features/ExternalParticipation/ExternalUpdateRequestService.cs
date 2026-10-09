using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.ExternalParticipation.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// WF-13's update requests (TASK-066). AHDA drafts a request for one project and one entity, issues it to a named responder of the entity
/// with a named reviewer of its own, and may cancel it while the entity has not answered. An external caller lists and reads only issued
/// requests its data scope reaches — its own entity's, on the projects its assignment covers — as the external projection.
/// </summary>
internal sealed class ExternalUpdateRequestService(
    IExternalParticipationRepository repository,
    ExternalParticipationGate gate,
    ExternalParticipationAccess access,
    ExternalParticipationReferences references,
    ExternalParticipationViews views,
    IAuditTrail audit,
    TimeProvider timeProvider) : IExternalUpdateRequestService
{
    public async Task<ExternalUpdateRequestPage> ListAsync(Guid callerId, ExternalUpdateRequestQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        RecordScope scope = await access.ViewScopeAsync(callerId, cancellationToken).ConfigureAwait(false);
        if (scope.IsEmpty)
        {
            return new ExternalUpdateRequestPage([], page.Page, page.PageSize, 0);
        }

        ParticipationAudience audience = await gate.AudienceAsync(callerId, cancellationToken).ConfigureAwait(false);
        (IReadOnlyList<ExternalUpdateRequest> items, int total) = await repository.PageRequestsAsync(
            scope, query, issuedOnly: audience == ParticipationAudience.External, page, cancellationToken).ConfigureAwait(false);
        return new ExternalUpdateRequestPage(await views.RequestsAsync(items, audience, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> GetAsync(Guid callerId, Guid requestId, CancellationToken cancellationToken)
    {
        LoadedRequest loaded = await gate.LoadRequestAsync(callerId, PermissionCatalogue.ExternalRequestView, requestId, null, ParticipationActor.Holder, cancellationToken)
            .ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(loaded.Request!, loaded.Audience, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> CreateAsync(Guid callerId, ExternalUpdateRequestDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        (ProjectFacts? project, AdministrationError? refused) = await gate.ReachProjectAsync(callerId, draft.ProjectId, draft.ExternalEntityId, cancellationToken).ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        AdministrationResult<ContributionSchema> schema = await references.SchemaAsync(draft.ContributionTypeItemId, cancellationToken).ConfigureAwait(false);
        AdministrationError? broken = ExternalParticipationReferences.ProjectRefused(project!)
                                      ?? await references.EntityRefusedAsync(draft.ExternalEntityId, cancellationToken).ConfigureAwait(false)
                                      ?? schema.Error
                                      ?? await references.TargetRefusedAsync(schema.Value!, draft.TargetId, project!, cancellationToken).ConfigureAwait(false)
                                      ?? await references.PeopleRefusedAsync(project!, draft.ExternalEntityId, draft.ResponsibleUserId, draft.ReviewerUserId, cancellationToken)
                                          .ConfigureAwait(false);
        if (broken is not null)
        {
            return broken;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ExternalUpdateRequest request = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project!.Id,
            ExternalEntityId = draft.ExternalEntityId,
            Origin = ExternalRequestOrigin.AhdaIssued,
            ContributionTypeItemId = draft.ContributionTypeItemId,
            ContributionSchemaCode = schema.Value!.Code,
            TargetModule = schema.Value.TargetModule,
            TargetType = schema.Value.TargetType,
            TargetId = draft.TargetId,
            Instructions = draft.Instructions,
            ResponsibleUserId = draft.ResponsibleUserId,
            ReviewerUserId = draft.ReviewerUserId,
            DueDate = draft.DueDate,
            Status = ExternalUpdateRequestStatus.Draft,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(request);
        audit.Stage(ExternalParticipationAudit.RequestCreated(callerId, request));
        return await SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> UpdateAsync(
        Guid callerId, Guid requestId, ExternalUpdateRequestChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        LoadedRequest loaded = await gate.LoadRequestAsync(callerId, PermissionCatalogue.ExternalRequestManage, requestId, expectedVersion, ParticipationActor.Holder, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ExternalUpdateRequest request) = (loaded.Project!, loaded.Request!);
        if (request.Status != ExternalUpdateRequestStatus.Draft)
        {
            return AdministrationError.Conflict(ExternalParticipationErrorCodes.RequestNotEditable);
        }

        AdministrationResult<ContributionSchema> schema = await references.SchemaAsync(changes.ContributionTypeItemId, cancellationToken).ConfigureAwait(false);
        AdministrationError? broken = ExternalParticipationReferences.ProjectRefused(project)
                                      ?? schema.Error
                                      ?? await references.TargetRefusedAsync(schema.Value!, changes.TargetId, project, cancellationToken).ConfigureAwait(false)
                                      ?? await references.PeopleRefusedAsync(project, request.ExternalEntityId, changes.ResponsibleUserId, changes.ReviewerUserId, cancellationToken)
                                          .ConfigureAwait(false);
        if (broken is not null)
        {
            return broken;
        }

        RequestFields before = RequestFields.Of(request);
        request.ContributionTypeItemId = changes.ContributionTypeItemId;
        request.ContributionSchemaCode = schema.Value!.Code;
        request.TargetModule = schema.Value.TargetModule;
        request.TargetType = schema.Value.TargetType;
        request.TargetId = changes.TargetId;
        request.Instructions = changes.Instructions;
        request.ResponsibleUserId = changes.ResponsibleUserId;
        request.ReviewerUserId = changes.ReviewerUserId;
        request.DueDate = changes.DueDate;
        ExternalParticipationGate.Touch(request, callerId, timeProvider.GetUtcNow());

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(ExternalParticipationAudit.RequestChanged(callerId, before, request));
        return await SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid requestId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        LoadedRequest loaded = await gate.LoadRequestAsync(callerId, PermissionCatalogue.ExternalRequestManage, requestId, expectedVersion, ParticipationActor.Holder, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ExternalUpdateRequest request = loaded.Request!;
        if (request.Status != ExternalUpdateRequestStatus.Draft)
        {
            return AdministrationError.Conflict(ExternalParticipationErrorCodes.RequestNotEditable);
        }

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Remove(request);
        audit.Stage(ExternalParticipationAudit.RequestDeleted(callerId, request));
        return await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == ExternalParticipationSaveOutcome.Saved ? null : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> IssueAsync(Guid callerId, Guid requestId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        LoadedRequest loaded = await gate.LoadRequestAsync(callerId, PermissionCatalogue.ExternalRequestManage, requestId, expectedVersion, ParticipationActor.Holder, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ExternalUpdateRequest request) = (loaded.Project!, loaded.Request!);
        if (request.Status != ExternalUpdateRequestStatus.Draft)
        {
            return ExternalParticipationWorkflow.IsFinal(request.Status) ? AdministrationError.TerminalState : AdministrationError.InvalidTransition;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        AdministrationResult<ContributionSchema> schema = await references.SchemaAsync(request.ContributionTypeItemId, cancellationToken).ConfigureAwait(false);
        AdministrationError? broken = ExternalParticipationReferences.ProjectRefused(project)
                                      ?? await references.EntityRefusedAsync(request.ExternalEntityId, cancellationToken).ConfigureAwait(false)
                                      ?? schema.Error
                                      ?? (schema.Value!.Code == request.ContributionSchemaCode
                                          ? null
                                          : AdministrationError.Rule(ExternalParticipationErrorCodes.SchemaInvalid, new FieldIssue("contributionTypeItemId", FieldIssue.NotAllowed)))
                                      ?? await references.TargetRefusedAsync(schema.Value, request.TargetId, project, cancellationToken).ConfigureAwait(false)
                                      ?? Incomplete(request)
                                      ?? await references.PeopleRefusedAsync(project, request.ExternalEntityId, request.ResponsibleUserId, request.ReviewerUserId, cancellationToken)
                                          .ConfigureAwait(false)
                                      ?? ExternalParticipationReferences.DueDateRefused(request.DueDate, now);
        if (broken is not null)
        {
            return broken;
        }

        AdministrationResult<ResolvedConfiguration> participation =
            await references.ParticipationAsync(project, request.ContributionTypeItemId, now, cancellationToken).ConfigureAwait(false);
        if (!participation.Succeeded)
        {
            return participation.Error;
        }

        request.Status = ExternalUpdateRequestStatus.Issued;
        request.ParticipationConfigurationVersionId = participation.Value.VersionId;
        request.IssuedByUserId = callerId;
        request.IssuedAt = now;
        ExternalParticipationGate.Touch(request, callerId, now);

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(ExternalParticipationAudit.RequestTransition(ExternalParticipationAuditEvents.RequestIssued, callerId, ExternalUpdateRequestStatus.Draft, request));
        return await SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> CancelAsync(
        Guid callerId, Guid requestId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        LoadedRequest loaded = await gate.LoadRequestAsync(callerId, PermissionCatalogue.ExternalRequestManage, requestId, expectedVersion, ParticipationActor.Holder, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ExternalUpdateRequest request = loaded.Request!;
        ExternalUpdateRequestStatus from = request.Status;
        AdministrationError? notCancellable = from switch
        {
            ExternalUpdateRequestStatus.Issued or ExternalUpdateRequestStatus.InProgress => null,
            ExternalUpdateRequestStatus.Responded => AdministrationError.Conflict(ExternalParticipationErrorCodes.RequestResponded),
            ExternalUpdateRequestStatus.Closed or ExternalUpdateRequestStatus.Cancelled => AdministrationError.TerminalState,
            ExternalUpdateRequestStatus.Draft => AdministrationError.InvalidTransition,
            _ => throw new ArgumentOutOfRangeException(nameof(requestId), from, "Unknown request status."),
        };
        if (notCancellable is not null)
        {
            return notCancellable;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        request.Status = ExternalUpdateRequestStatus.Cancelled;
        request.CancelledAt = now;
        request.CancellationReason = reason;
        ExternalParticipationGate.Touch(request, callerId, now);

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(ExternalParticipationAudit.RequestTransition(ExternalParticipationAuditEvents.RequestCancelled, callerId, from, request));
        return await SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    public Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> AssignResponderAsync(
        Guid callerId, Guid requestId, Guid responsibleUserId, uint? expectedVersion, CancellationToken cancellationToken) =>
        AssignAsync(callerId, requestId, expectedVersion, responder: responsibleUserId, reviewer: null, cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> AssignReviewerAsync(
        Guid callerId, Guid requestId, Guid reviewerUserId, uint? expectedVersion, CancellationToken cancellationToken) =>
        AssignAsync(callerId, requestId, expectedVersion, responder: null, reviewer: reviewerUserId, cancellationToken);

    /// <summary>WF-13 §9.4: one of the two people replaced on an issued request, the replacement eligible now. Earlier revisions keep their contributor.</summary>
    private async Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> AssignAsync(
        Guid callerId, Guid requestId, uint? expectedVersion, Guid? responder, Guid? reviewer, CancellationToken cancellationToken)
    {
        LoadedRequest loaded = await gate.LoadRequestAsync(callerId, PermissionCatalogue.ExternalRequestManage, requestId, expectedVersion, ParticipationActor.Holder, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ExternalUpdateRequest request) = (loaded.Project!, loaded.Request!);
        if (!ExternalParticipationWorkflow.IsAssignable(request.Status))
        {
            return request.Status == ExternalUpdateRequestStatus.Draft
                ? AdministrationError.InvalidTransition
                : AdministrationError.TerminalState;
        }

        if (await references.PeopleRefusedAsync(project, request.ExternalEntityId, responder, reviewer, cancellationToken).ConfigureAwait(false) is { } ineligible)
        {
            return ineligible;
        }

        AuditEntry entry = responder is { } newResponder
            ? ExternalParticipationAudit.Assigned(
                ExternalParticipationAuditEvents.ResponderAssigned, ExternalParticipationAuditAttributes.ResponsibleUserId, callerId, request.ResponsibleUserId, newResponder, request)
            : ExternalParticipationAudit.Assigned(
                ExternalParticipationAuditEvents.ReviewerAssigned, ExternalParticipationAuditAttributes.ReviewerUserId, callerId, request.ReviewerUserId, reviewer!.Value, request);
        request.ResponsibleUserId = responder ?? request.ResponsibleUserId;
        request.ReviewerUserId = reviewer ?? request.ReviewerUserId;
        ExternalParticipationGate.Touch(request, callerId, timeProvider.GetUtcNow());

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(entry);
        return await SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>422 <c>EXTERNAL_REQUEST_INCOMPLETE</c>: a request is issued to a named responder, with a named reviewer.</summary>
    private static AdministrationError? Incomplete(ExternalUpdateRequest request)
    {
        FieldIssue[] missing =
        [
            .. request.ResponsibleUserId is null ? [new FieldIssue("responsibleUserId", FieldIssue.Required)] : Array.Empty<FieldIssue>(),
            .. request.ReviewerUserId is null ? [new FieldIssue("reviewerUserId", FieldIssue.Required)] : Array.Empty<FieldIssue>(),
        ];
        return missing.Length > 0 ? AdministrationError.Rule(ExternalParticipationErrorCodes.RequestIncomplete, missing) : null;
    }

    /// <summary>Saves, commits and answers with the request as now saved, in AHDA's view: only AHDA writes a request. A row changed meanwhile is 412.</summary>
    private async Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> SaveRequestAsync(
        IExternalParticipationWork work, ExternalUpdateRequest request, CancellationToken cancellationToken) =>
        await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == ExternalParticipationSaveOutcome.Saved
            ? await VersionedAsync(request, ParticipationAudience.Internal, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;

    private async Task<Versioned<ExternalUpdateRequestDetail>> VersionedAsync(ExternalUpdateRequest request, ParticipationAudience audience, CancellationToken cancellationToken) =>
        new(await views.RequestAsync(request, audience, cancellationToken).ConfigureAwait(false), repository.RowVersionOf(request));
}
