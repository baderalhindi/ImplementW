using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// WF-08's register (TASK-060): change requests raised and edited by the project's people — an entity Project Manager included
/// (ADR-013) — while they are with their requester, and the materiality preview a requester sees before submitting. A request's state
/// moves only through <see cref="ChangeRequestLifecycleService"/>.
/// </summary>
internal sealed class ChangeRequestService(
    IChangeRequestRepository repository, ChangeRequestGate gate, ChangeRequestReferences references, ChangeRequestViews views, MaterialityEvaluator materiality,
    IAuditTrail audit, TimeProvider timeProvider) : IChangeRequestService
{
    public async Task<ChangeRequestPage> ListAsync(Guid callerId, ChangeRequestQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, query.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new ChangeRequestPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ChangeRequestEntity> items, int total) = await repository.PageAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new ChangeRequestPage(await views.DetailsAsync(items, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ChangeRequestDetail>>> GetAsync(Guid callerId, Guid changeRequestId, CancellationToken cancellationToken)
    {
        LoadedChangeRequest loaded = await gate.LoadAsync(callerId, PermissionCatalogue.ChangeRequestView, changeRequestId, null, internalOnly: false, cancellationToken)
            .ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await gate.VersionedAsync(loaded.Request!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ChangeRequestDetail>>> CreateAsync(Guid callerId, ChangeRequestDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        (ProjectFacts? project, AdministrationError? refused) = await gate.ReachProjectAsync(callerId, PermissionCatalogue.ChangeRequestRaise, draft.ProjectId, cancellationToken)
            .ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        if ((ChangeRequestReferences.RaisingRefused(project!)
             ?? await references.ProfileRefusedAsync(project!, draft.Fields.RequestedGovernanceProfileItemId, cancellationToken).ConfigureAwait(false)) is { } broken)
        {
            return broken;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ChangeRequestEntity request = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project!.Id,
            ChangeType = draft.ChangeType,
            Status = ChangeRequestStatus.Draft,
            RequestedByUserId = callerId,
            Title = draft.Fields.Title,
            Justification = draft.Fields.Justification,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        Apply(request, draft.Fields);

        await using IChangeRequestWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(request);
        audit.Stage(ChangeRequestAudit.Created(callerId, project, request));
        return await gate.SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ChangeRequestDetail>>> UpdateAsync(
        Guid callerId, Guid changeRequestId, ChangeRequestChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        LoadedChangeRequest loaded = await gate.LoadAsync(callerId, PermissionCatalogue.ChangeRequestRaise, changeRequestId, expectedVersion, internalOnly: false, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ChangeRequestEntity request) = (loaded.Project!, loaded.Request!);
        AdministrationError? broken = EditRefused(request)
                                      ?? ChangeRequestReferences.RaisingRefused(project)
                                      ?? (changes.RequestedGovernanceProfileItemId == request.RequestedGovernanceProfileItemId ? null
                                          : await references.ProfileRefusedAsync(project, changes.RequestedGovernanceProfileItemId, cancellationToken).ConfigureAwait(false));
        if (broken is not null)
        {
            return broken;
        }

        ChangeRequestFields before = ChangeRequestFields.Of(request);
        Apply(request, changes);
        ChangeRequestGate.Touch(request, callerId, timeProvider.GetUtcNow());
        await using IChangeRequestWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(ChangeRequestAudit.Changed(callerId, project, before, request));
        return await gate.SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        LoadedChangeRequest loaded = await gate.LoadAsync(callerId, PermissionCatalogue.ChangeRequestRaise, changeRequestId, expectedVersion, internalOnly: false, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        // HARD_DRAFT, and only a draft never submitted (WF-08 BR-CHG-037): a submitted request is history, withdrawn rather than removed.
        (ProjectFacts project, ChangeRequestEntity request) = (loaded.Project!, loaded.Request!);
        if (request.Status != ChangeRequestStatus.Draft)
        {
            return AdministrationError.Conflict(ChangeRequestErrorCodes.NotEditable);
        }

        await using IChangeRequestWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(ChangeRequestAudit.Deleted(callerId, project, request));
        repository.Remove(request);
        return await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == ChangeRequestSaveOutcome.Saved ? null : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<MaterialityAssessment>> PreviewMaterialityAsync(Guid callerId, Guid changeRequestId, CancellationToken cancellationToken)
    {
        LoadedChangeRequest loaded = await gate.LoadAsync(callerId, PermissionCatalogue.ChangeRequestView, changeRequestId, null, internalOnly: false, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        if (!ChangeRequestWorkflow.IsBeforeReview(loaded.Request!.Status))
        {
            return AdministrationError.Conflict(ChangeRequestErrorCodes.Evaluated);
        }

        AdministrationResult<MaterialityEvaluation> evaluated = await materiality.EvaluateAsync(
            loaded.Project!, loaded.Request, callerId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return evaluated.Succeeded ? MaterialityEvaluator.ToAssessment(evaluated.Value, recorded: false) : evaluated.Error;
    }

    /// <summary>A REJECTED, WITHDRAWN or CLOSED request changes no more; one past its requester keeps the fields it was submitted with.</summary>
    public static AdministrationError? EditRefused(ChangeRequestEntity request) =>
        ChangeRequestWorkflow.IsFinal(request.Status) ? AdministrationError.TerminalState
        : ChangeRequestWorkflow.IsEditable(request.Status) ? null
        : AdministrationError.Conflict(ChangeRequestErrorCodes.NotEditable);

    private static void Apply(ChangeRequestEntity request, ChangeRequestChanges fields)
    {
        request.Title = fields.Title;
        request.Justification = fields.Justification;
        request.CostImpactSar = fields.CostImpactSar;
        request.ScheduleImpactDays = fields.ScheduleImpactDays;
        request.ScopeImpact = fields.ScopeImpact;
        request.IsContractualObligation = fields.IsContractualObligation;
        request.RequestedGovernanceProfileItemId = fields.RequestedGovernanceProfileItemId;
    }
}
