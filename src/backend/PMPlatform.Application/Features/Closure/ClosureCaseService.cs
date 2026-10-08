using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// WF-10's closure stage (TASK-063): closure cases raised and edited by the project's people while they are with their requester. The
/// path is set by the project's state when the case is raised and never changes: a COMPLETED project's case follows its effected
/// completion case; a SUSPENDED project's case is the terminal path, with no completion (WF-10 §9). A second open case is refused by the
/// open-case key (CLO-CC-06). A case's state moves only through <see cref="CloseoutCommands"/>.
/// </summary>
internal sealed class ClosureCaseService(
    ICloseoutRepository repository, CloseoutGate gate, CloseoutViews views, CloseoutCommands commands, IAuditTrail audit, TimeProvider timeProvider)
    : IClosureCaseService
{
    public async Task<ClosureCasePage> ListAsync(Guid callerId, CloseoutCaseQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, query.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new ClosureCasePage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ClosureCase> items, int total) = await repository.PageCasesAsync<ClosureCase>(query, page, cancellationToken).ConfigureAwait(false);
        return new ClosureCasePage(await views.ClosureDetailsAsync(items, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ClosureCaseDetail>>> GetAsync(Guid callerId, Guid closureCaseId, CancellationToken cancellationToken)
    {
        Loaded<ClosureCase> loaded = await gate.LoadCaseAsync<ClosureCase>(callerId, PermissionCatalogue.CloseoutView, closureCaseId, null, false, cancellationToken)
            .ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(loaded.Record!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ClosureCaseDetail>>> CreateAsync(Guid callerId, ClosureCaseDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        (ProjectFacts? project, AdministrationError? refused) = await gate.ReachProjectAsync(callerId, PermissionCatalogue.CloseoutRaise, draft.ProjectId, cancellationToken)
            .ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        // The normal path follows the project's effected completion; the terminal path is a SUSPENDED project's, with none (BR-CLO-003).
        CompletionCase? completed = project!.Status == ProjectLifecycleState.Completed
            ? await repository.FindEffectedCompletionAsync(project.Id, cancellationToken).ConfigureAwait(false)
            : null;
        if (!(completed is not null || project.Status == ProjectLifecycleState.Suspended))
        {
            return AdministrationError.Rule(ClosureErrorCodes.ProjectNotEligible);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ClosureCase closure = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            CompletionCaseId = completed?.Id,
            Status = CloseoutCaseStatus.Draft,
            RequestedByUserId = callerId,
            ClosureNarrative = draft.Fields.ClosureNarrative,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };

        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(closure);
        audit.Stage(CloseoutAudit.Created(callerId, project, closure));
        return await SaveAsync(work, closure, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ClosureCaseDetail>>> UpdateAsync(
        Guid callerId, Guid closureCaseId, ClosureCaseChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Loaded<ClosureCase> loaded = await gate.LoadCaseAsync<ClosureCase>(
            callerId, PermissionCatalogue.CloseoutRaise, closureCaseId, expectedVersion, false, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ClosureCase closure) = (loaded.Project!, loaded.Record!);
        if ((CloseoutCases.EditRefused(closure) ?? CloseoutEligibility.ProjectRefused(closure, project)) is { } broken)
        {
            return broken;
        }

        CloseoutFields before = CloseoutFields.Of(closure);
        closure.ClosureNarrative = changes.ClosureNarrative;
        CloseoutGate.Touch(closure, callerId, timeProvider.GetUtcNow());
        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(CloseoutAudit.Changed(callerId, project, before, closure));
        return await SaveAsync(work, closure, cancellationToken).ConfigureAwait(false);
    }

    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        CloseoutCases.DeleteAsync<ClosureCase>(repository, gate, audit, callerId, closureCaseId, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> EvaluateReadinessAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, closureCaseId, CloseoutCommand.EvaluateReadiness, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> WaiveCheckAsync(
        Guid callerId, Guid closureCaseId, ReadinessWaiver waiver, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, closureCaseId, CloseoutCommand.WaiveCheck, expectedVersion, cancellationToken, waiver);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> SubmitAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, closureCaseId, CloseoutCommand.Submit, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> WithdrawAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, closureCaseId, CloseoutCommand.Withdraw, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> StartReviewAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, closureCaseId, CloseoutCommand.StartReview, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ClosureCaseDetail>>> ActivateAsync(Guid callerId, Guid closureCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, closureCaseId, CloseoutCommand.Activate, expectedVersion, cancellationToken);

    private async Task<AdministrationResult<Versioned<ClosureCaseDetail>>> RunAsync(
        Guid callerId, Guid closureCaseId, CloseoutCommand command, uint? expectedVersion, CancellationToken cancellationToken, ReadinessWaiver? waiver = null)
    {
        AdministrationResult<ClosureCase> ran = await commands.RunAsync<ClosureCase>(callerId, closureCaseId, command, expectedVersion, cancellationToken, waiver)
            .ConfigureAwait(false);
        return ran.Succeeded ? await VersionedAsync(ran.Value, cancellationToken).ConfigureAwait(false) : ran.Error;
    }

    private async Task<AdministrationResult<Versioned<ClosureCaseDetail>>> SaveAsync(ICloseoutWork work, ClosureCase closure, CancellationToken cancellationToken)
    {
        CloseoutSaveOutcome saved = await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false);
        return saved == CloseoutSaveOutcome.Saved ? await VersionedAsync(closure, cancellationToken).ConfigureAwait(false) : CloseoutGate.RefusalOf(saved);
    }

    private async Task<Versioned<ClosureCaseDetail>> VersionedAsync(ClosureCase closure, CancellationToken cancellationToken) =>
        new((await views.ClosureDetailsAsync([closure], cancellationToken).ConfigureAwait(false))[0], repository.RowVersionOf(closure));
}
