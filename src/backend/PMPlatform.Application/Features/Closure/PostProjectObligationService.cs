using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// WF-10's post-project obligations (TASK-063; WF-10 §7.2): recorded against the project's completion case — open while it is prepared, or
/// effected, so they outlive the completion — or against an open terminal closure case. They stay open while the project is COMPLETED and
/// are settled before it closes: completion requires each open one to have an owner and a due date, closure each to be settled
/// (BR-CLO-023). Waiving one is AHDA's (ADR-013). Once the project is CLOSED none changes (BR-CLO-020).
/// </summary>
internal sealed class PostProjectObligationService(
    ICloseoutRepository repository, CloseoutGate gate, CloseoutAccess access, IAuditTrail audit, TimeProvider timeProvider) : IPostProjectObligationService
{
    public async Task<PostProjectObligationPage> ListAsync(Guid callerId, PostProjectObligationQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, query.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new PostProjectObligationPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<PostProjectObligation> items, int total) = await repository.PageObligationsAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new PostProjectObligationPage([.. items.Select(CloseoutViews.ToDetail)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> GetAsync(Guid callerId, Guid obligationId, CancellationToken cancellationToken)
    {
        Loaded<PostProjectObligation> loaded = await gate.LoadObligationAsync(callerId, PermissionCatalogue.CloseoutView, obligationId, null, false, cancellationToken)
            .ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : Versioned(loaded.Record!);
    }

    public async Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> CreateAsync(Guid callerId, PostProjectObligationDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CloseoutCase? @case = draft.CompletionCaseId is { } completionId
            ? await repository.FindCaseAsync<CompletionCase>(completionId, null, cancellationToken).ConfigureAwait(false)
            : await repository.FindCaseAsync<ClosureCase>(draft.ClosureCaseId!.Value, null, cancellationToken).ConfigureAwait(false);
        if (@case is null)
        {
            return AdministrationError.NotFound;
        }

        (ProjectFacts? project, AdministrationError? refused) = await gate.ReachProjectAsync(callerId, PermissionCatalogue.CloseoutRaise, @case.ProjectId, cancellationToken)
            .ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        if ((CaseRefused(@case) ?? await OwnerRefusedAsync(draft.Fields.OwnerUserId, cancellationToken).ConfigureAwait(false)) is { } broken)
        {
            return broken;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        PostProjectObligation obligation = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = @case.ProjectId,
            CompletionCaseId = draft.CompletionCaseId,
            ClosureCaseId = draft.ClosureCaseId,
            Title = draft.Fields.Title,
            Status = PostProjectObligationStatus.Open,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        Apply(obligation, draft.Fields);

        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(obligation);
        audit.Stage(CloseoutAudit.ObligationCreated(callerId, project!, obligation));
        return await SaveAsync(work, obligation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> UpdateAsync(
        Guid callerId, Guid obligationId, PostProjectObligationChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Loaded<PostProjectObligation> loaded = await gate.LoadObligationAsync(callerId, PermissionCatalogue.CloseoutRaise, obligationId, expectedVersion, false, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        PostProjectObligation obligation = loaded.Record!;
        AdministrationError? broken = PostProjectObligationRules.IsOpen(obligation.Status)
            ? await OwnerRefusedAsync(changes.OwnerUserId, cancellationToken).ConfigureAwait(false)
            : AdministrationError.Conflict(ClosureErrorCodes.ObligationSettled);
        if (broken is not null)
        {
            return broken;
        }

        PostProjectObligationChanges before = new(obligation.Title, obligation.Description, obligation.OwnerUserId, obligation.DueDate);
        Apply(obligation, changes);
        CloseoutGate.Touch(obligation, callerId, timeProvider.GetUtcNow());
        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(CloseoutAudit.ObligationChanged(callerId, loaded.Project!, before, obligation));
        return await SaveAsync(work, obligation, cancellationToken).ConfigureAwait(false);
    }

    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> StartAsync(Guid callerId, Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, obligationId, ObligationCommand.Start, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> SatisfyAsync(Guid callerId, Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, obligationId, ObligationCommand.Satisfy, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> CancelAsync(Guid callerId, Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, obligationId, ObligationCommand.Cancel, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> WaiveAsync(Guid callerId, Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, obligationId, ObligationCommand.Waive, expectedVersion, cancellationToken);

    private async Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> MoveAsync(
        Guid callerId, Guid obligationId, ObligationCommand command, uint? expectedVersion, CancellationToken cancellationToken)
    {
        bool waive = command == ObligationCommand.Waive;
        Loaded<PostProjectObligation> loaded = await gate.LoadObligationAsync(
            callerId, waive ? PermissionCatalogue.CloseoutWaive : PermissionCatalogue.CloseoutRaise, obligationId, expectedVersion, waive, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        PostProjectObligation obligation = loaded.Record!;
        PostProjectObligationStatus from = obligation.Status;
        if (PostProjectObligationRules.TargetOf(command, from) is not { } to)
        {
            return PostProjectObligationRules.IsOpen(from) ? AdministrationError.InvalidTransition : AdministrationError.Conflict(ClosureErrorCodes.ObligationSettled);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        obligation.Status = to;
        obligation.SatisfiedAt = to == PostProjectObligationStatus.Satisfied ? now : null;
        CloseoutGate.Touch(obligation, callerId, now);
        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(CloseoutAudit.ObligationTransitioned(callerId, loaded.Project!, from, obligation));
        return await SaveAsync(work, obligation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// An obligation is recorded against its project's completion case — still being prepared, or effected so it outlives the completion —
    /// or against an open closure case on the terminal path, which has no completion.
    /// </summary>
    private static AdministrationError? CaseRefused(CloseoutCase @case) => @case switch
    {
        CompletionCase { Status: CloseoutCaseStatus.Rejected or CloseoutCaseStatus.Withdrawn }
            => AdministrationError.Rule(ClosureErrorCodes.ObligationCaseInvalid, new FieldIssue("completionCaseId", FieldIssue.NotAllowed)),
        CompletionCase => null,
        ClosureCase { CompletionCaseId: null } closure when !CloseoutWorkflow.IsFinal(closure.Status) => null,
        _ => AdministrationError.Rule(ClosureErrorCodes.ObligationCaseInvalid, new FieldIssue("closureCaseId", FieldIssue.NotAllowed)),
    };

    private async Task<AdministrationError?> OwnerRefusedAsync(Guid? ownerUserId, CancellationToken cancellationToken) =>
        ownerUserId is { } owner && !await access.IsActiveUserAsync(owner, cancellationToken).ConfigureAwait(false)
            ? AdministrationError.Rule(ClosureErrorCodes.ObligationOwnerInvalid, new FieldIssue("ownerUserId", FieldIssue.NotFound))
            : null;

    private async Task<AdministrationResult<Versioned<PostProjectObligationDetail>>> SaveAsync(ICloseoutWork work, PostProjectObligation obligation, CancellationToken cancellationToken)
    {
        CloseoutSaveOutcome saved = await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false);
        return saved == CloseoutSaveOutcome.Saved ? Versioned(obligation) : CloseoutGate.RefusalOf(saved);
    }

    private Versioned<PostProjectObligationDetail> Versioned(PostProjectObligation obligation) => new(CloseoutViews.ToDetail(obligation), repository.RowVersionOf(obligation));

    private static void Apply(PostProjectObligation obligation, PostProjectObligationChanges fields)
    {
        obligation.Title = fields.Title;
        obligation.Description = fields.Description;
        obligation.OwnerUserId = fields.OwnerUserId;
        obligation.DueDate = fields.DueDate;
    }
}
