using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// WF-07's register (TASK-057): issues and challenges raised by the project's people — entities on their own project included
/// (ADR-013) — and edited by AHDA's side. A concern's state moves only through <see cref="ConcernLifecycleService"/>.
/// </summary>
internal sealed class ManagementConcernService(
    IManagementConcernRepository repository, ConcernGate gate, ConcernIntake intake, ConcernReferences references, ConcernViews views, IAuditTrail audit,
    TimeProvider timeProvider) : IManagementConcernService
{
    public async Task<ConcernPage> ListAsync(Guid callerId, ConcernQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, query.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new ConcernPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ConcernEntity> items, int total) = await repository.PageAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new ConcernPage(await views.DetailsAsync(items, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ConcernDetail>>> GetAsync(Guid callerId, Guid concernId, CancellationToken cancellationToken)
    {
        LoadedConcern loaded = await gate.LoadAsync(callerId, PermissionCatalogue.ConcernView, concernId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused
            ? refused
            : new Versioned<ConcernDetail>(await views.DetailAsync(loaded.Concern!, cancellationToken).ConfigureAwait(false), repository.RowVersionOf(loaded.Concern!));
    }

    public async Task<AdministrationResult<Versioned<ConcernDetail>>> RaiseAsync(Guid callerId, ConcernDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        (ProjectFacts? project, AdministrationError? refused) = await gate.ReachProjectAsync(callerId, PermissionCatalogue.ConcernRaise, draft.ProjectId, cancellationToken).ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        await using IConcernWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        AdministrationResult<ConcernEntity> raised = await intake.StageAsync(callerId, project!, draft, null, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return raised.Succeeded ? await gate.SaveConcernAsync(work, raised.Value, cancellationToken).ConfigureAwait(false) : raised.Error;
    }

    public async Task<AdministrationResult<Versioned<ConcernDetail>>> UpdateAsync(
        Guid callerId, Guid concernId, ConcernChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        LoadedConcern loaded = await gate.LoadInternalAsync(callerId, PermissionCatalogue.ConcernManage, concernId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        ConcernEntity concern = loaded.Concern!;
        DateTimeOffset now = timeProvider.GetUtcNow();

        // A reference the concern already holds stays valid: a category since retired does not block an edit of the title.
        AdministrationError? broken = ConcernReferences.ChangeRefused(project)
                                      ?? EditRefused(concern)
                                      ?? (changes.TargetResolutionDate == concern.TargetResolutionDate ? null : ConcernReferences.TargetDateRefused(changes.TargetResolutionDate, now))
                                      ?? (changes.CategoryItemId == concern.CategoryItemId ? null
                                          : await references.CategoryRefusedAsync(changes.CategoryItemId, cancellationToken).ConfigureAwait(false))
                                      ?? (changes.PriorityItemId == concern.PriorityItemId ? null
                                          : await references.PriorityRefusedAsync(changes.PriorityItemId, cancellationToken).ConfigureAwait(false));
        if (broken is not null)
        {
            return broken;
        }

        ConcernFields before = ConcernFields.Of(concern);
        concern.Title = changes.Title;
        concern.Description = changes.Description;
        concern.CategoryItemId = changes.CategoryItemId;
        concern.PriorityItemId = changes.PriorityItemId;
        concern.TargetResolutionDate = changes.TargetResolutionDate;
        ConcernGate.Touch(concern, callerId, now);
        await using IConcernWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(ConcernAudit.Changed(callerId, project, before, concern));
        return await gate.SaveConcernAsync(work, concern, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A CLOSED concern changes no more; one with validation, or validated, keeps the fields it was submitted with.</summary>
    public static AdministrationError? EditRefused(ConcernEntity concern) =>
        concern.Status == Domain.ManagementConcern.ConcernStatus.Closed ? AdministrationError.Conflict(ConcernErrorCodes.Closed)
        : ConcernWorkflow.IsEditable(concern.Status) ? null
        : AdministrationError.Conflict(ConcernErrorCodes.NotEditable);
}
