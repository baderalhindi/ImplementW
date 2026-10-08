using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// WF-10's completion stage (TASK-063): completion cases raised and edited by the project's people — an entity Project Manager included
/// (ADR-013) — while they are with their requester. A second open case for the project is refused by the open-case key, however close
/// together the two arrive (CLO-CC-05). A case's state moves only through <see cref="CloseoutCommands"/>.
/// </summary>
internal sealed class CompletionCaseService(
    ICloseoutRepository repository, CloseoutGate gate, CloseoutViews views, CloseoutCommands commands, IAuditTrail audit, TimeProvider timeProvider)
    : ICompletionCaseService
{
    public async Task<CompletionCasePage> ListAsync(Guid callerId, CloseoutCaseQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, query.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new CompletionCasePage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<CompletionCase> items, int total) = await repository.PageCasesAsync<CompletionCase>(query, page, cancellationToken).ConfigureAwait(false);
        return new CompletionCasePage(await views.CompletionDetailsAsync(items, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<CompletionCaseDetail>>> GetAsync(Guid callerId, Guid completionCaseId, CancellationToken cancellationToken)
    {
        Loaded<CompletionCase> loaded = await gate.LoadCaseAsync<CompletionCase>(callerId, PermissionCatalogue.CloseoutView, completionCaseId, null, false, cancellationToken)
            .ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(loaded.Record!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<CompletionCaseDetail>>> CreateAsync(Guid callerId, CompletionCaseDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        (ProjectFacts? project, AdministrationError? refused) = await gate.ReachProjectAsync(callerId, PermissionCatalogue.CloseoutRaise, draft.ProjectId, cancellationToken)
            .ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        CompletionCase completion = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project!.Id,
            Status = CloseoutCaseStatus.Draft,
            RequestedByUserId = callerId,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        Apply(completion, draft.Fields);
        AdministrationError? broken = CloseoutEligibility.ProjectRefused(completion, project)
                                      ?? CloseoutEligibility.CompletionFieldsRefused(draft.Fields, project, DateOnly.FromDateTime(now.UtcDateTime));
        if (broken is not null)
        {
            return broken;
        }

        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(completion);
        audit.Stage(CloseoutAudit.Created(callerId, project, completion));
        return await SaveAsync(work, completion, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<CompletionCaseDetail>>> UpdateAsync(
        Guid callerId, Guid completionCaseId, CompletionCaseChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Loaded<CompletionCase> loaded = await gate.LoadCaseAsync<CompletionCase>(
            callerId, PermissionCatalogue.CloseoutRaise, completionCaseId, expectedVersion, false, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, CompletionCase completion) = (loaded.Project!, loaded.Record!);
        DateTimeOffset now = timeProvider.GetUtcNow();
        AdministrationError? broken = CloseoutCases.EditRefused(completion)
                                      ?? CloseoutEligibility.ProjectRefused(completion, project)
                                      ?? CloseoutEligibility.CompletionFieldsRefused(changes, project, DateOnly.FromDateTime(now.UtcDateTime));
        if (broken is not null)
        {
            return broken;
        }

        CloseoutFields before = CloseoutFields.Of(completion);
        Apply(completion, changes);
        CloseoutGate.Touch(completion, callerId, now);
        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(CloseoutAudit.Changed(callerId, project, before, completion));
        return await SaveAsync(work, completion, cancellationToken).ConfigureAwait(false);
    }

    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        CloseoutCases.DeleteAsync<CompletionCase>(repository, gate, audit, callerId, completionCaseId, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> EvaluateReadinessAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, completionCaseId, CloseoutCommand.EvaluateReadiness, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> WaiveCheckAsync(
        Guid callerId, Guid completionCaseId, ReadinessWaiver waiver, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, completionCaseId, CloseoutCommand.WaiveCheck, expectedVersion, cancellationToken, waiver);

    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> SubmitAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, completionCaseId, CloseoutCommand.Submit, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> WithdrawAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, completionCaseId, CloseoutCommand.Withdraw, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> StartReviewAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, completionCaseId, CloseoutCommand.StartReview, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<CompletionCaseDetail>>> ActivateAsync(Guid callerId, Guid completionCaseId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, completionCaseId, CloseoutCommand.Activate, expectedVersion, cancellationToken);

    private async Task<AdministrationResult<Versioned<CompletionCaseDetail>>> RunAsync(
        Guid callerId, Guid completionCaseId, CloseoutCommand command, uint? expectedVersion, CancellationToken cancellationToken, ReadinessWaiver? waiver = null)
    {
        AdministrationResult<CompletionCase> ran = await commands.RunAsync<CompletionCase>(callerId, completionCaseId, command, expectedVersion, cancellationToken, waiver)
            .ConfigureAwait(false);
        return ran.Succeeded ? await VersionedAsync(ran.Value, cancellationToken).ConfigureAwait(false) : ran.Error;
    }

    private async Task<AdministrationResult<Versioned<CompletionCaseDetail>>> SaveAsync(ICloseoutWork work, CompletionCase completion, CancellationToken cancellationToken)
    {
        CloseoutSaveOutcome saved = await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false);
        return saved == CloseoutSaveOutcome.Saved ? await VersionedAsync(completion, cancellationToken).ConfigureAwait(false) : CloseoutGate.RefusalOf(saved);
    }

    private async Task<Versioned<CompletionCaseDetail>> VersionedAsync(CompletionCase completion, CancellationToken cancellationToken) =>
        new((await views.CompletionDetailsAsync([completion], cancellationToken).ConfigureAwait(false))[0], repository.RowVersionOf(completion));

    private static void Apply(CompletionCase completion, CompletionCaseChanges fields)
    {
        completion.ActualProjectCompletionDate = fields.ActualProjectCompletionDate;
        completion.CompletionNarrative = fields.CompletionNarrative;
    }
}
