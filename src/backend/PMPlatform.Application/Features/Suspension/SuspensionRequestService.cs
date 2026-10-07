using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// WF-09's register (TASK-062): suspension and resumption requests raised and edited by the project's people — an entity Project
/// Manager included (ADR-013) — while they are with their requester, and the project's suspension periods. A second suspension request
/// for a project that is suspended, or that has one open already, is refused (BR-SUS-003, BR-SUS-004), and a unique key holds the same
/// rule against a concurrent raise. A request's state moves only through <see cref="SuspensionLifecycleService"/>.
/// </summary>
internal sealed class SuspensionRequestService(
    ISuspensionRepository repository, SuspensionGate gate, SuspensionEligibility eligibility, SuspensionViews views, IAuditTrail audit, TimeProvider timeProvider)
    : ISuspensionRequestService
{
    public async Task<SuspensionRequestPage> ListAsync(Guid callerId, SuspensionRequestQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, query.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new SuspensionRequestPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<SuspensionRequest> items, int total) = await repository.PageAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new SuspensionRequestPage(await views.DetailsAsync(items, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> GetAsync(Guid callerId, Guid suspensionRequestId, CancellationToken cancellationToken)
    {
        LoadedSuspensionRequest loaded = await gate.LoadAsync(callerId, PermissionCatalogue.SuspensionView, suspensionRequestId, null, internalOnly: false, cancellationToken)
            .ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await gate.VersionedAsync(loaded.Request!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> CreateAsync(Guid callerId, SuspensionRequestDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        (ProjectFacts? project, AdministrationError? refused) = await gate.ReachProjectAsync(callerId, PermissionCatalogue.SuspensionRaise, draft.ProjectId, cancellationToken)
            .ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        AdministrationError? broken = SuspensionEligibility.ProjectRefused(draft.RequestType, project!)
                                      ?? SuspensionEligibility.FieldsRefused(draft.RequestType, draft.Fields)
                                      ?? await eligibility.OpenRequestRefusedAsync(project!.Id, draft.RequestType, null, cancellationToken).ConfigureAwait(false);
        if (broken is not null)
        {
            return broken;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        SuspensionRequest request = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project!.Id,
            RequestType = draft.RequestType,
            Status = SuspensionRequestStatus.Draft,
            RequestedByUserId = callerId,
            Reason = draft.Fields.Reason,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        Apply(request, draft.Fields);

        await using ISuspensionWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(request);
        audit.Stage(SuspensionAudit.Created(callerId, project, request));
        return await gate.SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> UpdateAsync(
        Guid callerId, Guid suspensionRequestId, SuspensionRequestChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        LoadedSuspensionRequest loaded = await gate.LoadAsync(
            callerId, PermissionCatalogue.SuspensionRaise, suspensionRequestId, expectedVersion, internalOnly: false, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, SuspensionRequest request) = (loaded.Project!, loaded.Request!);
        AdministrationError? broken = EditRefused(request)
                                      ?? SuspensionEligibility.ProjectRefused(request.RequestType, project)
                                      ?? SuspensionEligibility.FieldsRefused(request.RequestType, changes);
        if (broken is not null)
        {
            return broken;
        }

        SuspensionRequestChanges before = new(request.Reason, request.RequestedEffectiveDate, request.PlannedResumptionDate);
        Apply(request, changes);
        SuspensionGate.Touch(request, callerId, timeProvider.GetUtcNow());
        await using ISuspensionWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(SuspensionAudit.Changed(callerId, project, before, request));
        return await gate.SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        LoadedSuspensionRequest loaded = await gate.LoadAsync(
            callerId, PermissionCatalogue.SuspensionRaise, suspensionRequestId, expectedVersion, internalOnly: false, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        // HARD_DRAFT, and only a draft never submitted: a submitted request is history, withdrawn rather than removed.
        (ProjectFacts project, SuspensionRequest request) = (loaded.Project!, loaded.Request!);
        if (request.Status != SuspensionRequestStatus.Draft)
        {
            return AdministrationError.Conflict(SuspensionErrorCodes.NotEditable);
        }

        await using ISuspensionWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(SuspensionAudit.Deleted(callerId, project, request));
        repository.Remove(request);
        return await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == SuspensionSaveOutcome.Saved ? null : AdministrationError.PreconditionFailed;
    }

    public async Task<ActiveSuspensionPage> ListSuspensionsAsync(Guid callerId, ActiveSuspensionQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, query.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new ActiveSuspensionPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ActiveSuspension> items, int total) = await repository.PageSuspensionsAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new ActiveSuspensionPage([.. items.Select(SuspensionViews.ToDetail)], page.Page, page.PageSize, total);
    }

    /// <summary>A REJECTED, WITHDRAWN or EFFECTED request changes no more; one past its requester keeps the fields it was submitted with.</summary>
    public static AdministrationError? EditRefused(SuspensionRequest request) =>
        SuspensionWorkflow.IsFinal(request.Status) ? AdministrationError.TerminalState
        : SuspensionWorkflow.IsEditable(request.Status) ? null
        : AdministrationError.Conflict(SuspensionErrorCodes.NotEditable);

    private static void Apply(SuspensionRequest request, SuspensionRequestChanges fields)
    {
        request.Reason = fields.Reason;
        request.RequestedEffectiveDate = fields.RequestedEffectiveDate;
        request.PlannedResumptionDate = fields.PlannedResumptionDate;
    }
}
