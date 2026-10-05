using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Application.Features.Risk.Contracts.Events;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// A risk's treatment and mitigation actions (TASK-055), changed while the risk is open. Each change also touches the risk, so
/// it is serialised with the risk's own commands: starting treatment never misses an action cancelled at the same moment.
/// </summary>
internal sealed class RiskTreatmentService(
    IRiskRepository repository, RiskGate gate, RiskReferences references, IAuditTrail audit, TimeProvider timeProvider) : IRiskTreatmentService
{
    public async Task<RiskTreatmentActionPage> ListActionsAsync(Guid callerId, Guid riskId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableRiskAsync(callerId, riskId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new RiskTreatmentActionPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<RiskTreatmentAction> items, int total) = await repository.PageActionsAsync(riskId, page, cancellationToken).ConfigureAwait(false);
        return new RiskTreatmentActionPage([.. items.Select(RiskViews.ToDetail)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> GetActionAsync(Guid callerId, Guid actionId, CancellationToken cancellationToken)
    {
        LoadedAction loaded = await LoadAsync(callerId, PermissionCatalogue.RiskView, actionId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused
            ? refused
            : new Versioned<RiskTreatmentActionDetail>(RiskViews.ToDetail(loaded.Action!), repository.RowVersionOf(loaded.Action!));
    }

    public async Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> CreateActionAsync(Guid callerId, RiskTreatmentActionDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        LoadedRisk loaded = await gate.LoadRiskAsync(callerId, PermissionCatalogue.RiskManage, draft.RiskId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        RiskEntity risk = loaded.Risk!;
        if (await RefusalAsync(project, risk, null, draft.OwnerUserId, cancellationToken).ConfigureAwait(false) is { } broken)
        {
            return broken;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        RiskTreatmentAction action = new()
        {
            Id = Guid.CreateVersion7(now),
            RiskId = risk.Id,
            Title = draft.Title,
            Description = draft.Description,
            ActionType = draft.ActionType,
            OwnerUserId = draft.OwnerUserId,
            DueDate = draft.DueDate,
            Status = RiskTreatmentActionStatus.Planned,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        await using IRiskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(action);
        RiskGate.Touch(risk, callerId, now);
        audit.Stage(RiskAudit.ActionCreated(callerId, project, action));
        return await SaveAsync(work, action, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> UpdateActionAsync(
        Guid callerId, Guid actionId, RiskTreatmentActionChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        LoadedAction loaded = await LoadAsync(callerId, PermissionCatalogue.RiskManage, actionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        RiskTreatmentAction action = loaded.Action!;
        Guid? newOwner = changes.OwnerUserId == action.OwnerUserId ? null : changes.OwnerUserId;
        if (await RefusalAsync(loaded.Project!, loaded.Risk!, action, newOwner, cancellationToken).ConfigureAwait(false) is { } broken)
        {
            return broken;
        }

        ActionPlan before = ActionPlan.Of(action);
        DateTimeOffset now = timeProvider.GetUtcNow();
        action.Title = changes.Title;
        action.Description = changes.Description;
        action.ActionType = changes.ActionType;
        action.OwnerUserId = changes.OwnerUserId;
        action.DueDate = changes.DueDate;
        RiskGate.Touch(action, callerId, now);
        RiskGate.Touch(loaded.Risk!, callerId, now);
        await using IRiskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(RiskAudit.ActionChanged(callerId, loaded.Project!, before, action));
        return await SaveAsync(work, action, cancellationToken).ConfigureAwait(false);
    }

    public Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> StartActionAsync(Guid callerId, Guid actionId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, actionId, RiskTreatmentActionStatus.InProgress, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> CompleteActionAsync(Guid callerId, Guid actionId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, actionId, RiskTreatmentActionStatus.Completed, expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> CancelActionAsync(Guid callerId, Guid actionId, uint? expectedVersion, CancellationToken cancellationToken) =>
        MoveAsync(callerId, actionId, RiskTreatmentActionStatus.Cancelled, expectedVersion, cancellationToken);

    /// <summary>
    /// An action's state machine: PLANNED → IN_PROGRESS → COMPLETED, and PLANNED or IN_PROGRESS → CANCELLED. Migration
    /// <c>TASK-055_GuardRiskHistory</c> refuses every other change of status in the database too.
    /// </summary>
    internal static bool Allows(RiskTreatmentActionStatus from, RiskTreatmentActionStatus to) => (from, to) switch
    {
        (RiskTreatmentActionStatus.Planned, RiskTreatmentActionStatus.InProgress) => true,
        (RiskTreatmentActionStatus.InProgress, RiskTreatmentActionStatus.Completed) => true,
        (RiskTreatmentActionStatus.Planned or RiskTreatmentActionStatus.InProgress, RiskTreatmentActionStatus.Cancelled) => true,
        _ => false,
    };

    internal static bool IsOpen(RiskTreatmentActionStatus status) => status is RiskTreatmentActionStatus.Planned or RiskTreatmentActionStatus.InProgress;

    private async Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> MoveAsync(
        Guid callerId, Guid actionId, RiskTreatmentActionStatus to, uint? expectedVersion, CancellationToken cancellationToken)
    {
        LoadedAction loaded = await LoadAsync(callerId, PermissionCatalogue.RiskManage, actionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        RiskTreatmentAction action = loaded.Action!;
        if (await RefusalAsync(loaded.Project!, loaded.Risk!, action, null, cancellationToken).ConfigureAwait(false) is { } broken)
        {
            return broken;
        }

        RiskTreatmentActionStatus from = action.Status;
        if (!Allows(from, to))
        {
            return AdministrationError.InvalidTransition;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        action.Status = to;
        action.CompletedAt = to == RiskTreatmentActionStatus.Completed ? now : null;
        RiskGate.Touch(action, callerId, now);
        RiskGate.Touch(loaded.Risk!, callerId, now);
        await using IRiskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        string eventType = to switch
        {
            RiskTreatmentActionStatus.InProgress => RiskAuditEvents.TreatmentActionStarted,
            RiskTreatmentActionStatus.Completed => RiskAuditEvents.TreatmentActionCompleted,
            RiskTreatmentActionStatus.Cancelled => RiskAuditEvents.TreatmentActionCancelled,
            RiskTreatmentActionStatus.Planned => throw new ArgumentOutOfRangeException(nameof(to), to, "An action is never moved back to PLANNED."),
            _ => throw new ArgumentOutOfRangeException(nameof(to), to, "Unknown action status."),
        };
        audit.Stage(RiskAudit.ActionTransition(eventType, callerId, loaded.Project!, from, action));
        return await SaveAsync(work, action, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The first rule a change breaks: the project's state, the risk open, the action open, the new owner's role.</summary>
    private async Task<AdministrationError?> RefusalAsync(ProjectFacts project, RiskEntity risk, RiskTreatmentAction? action, Guid? newOwnerUserId, CancellationToken cancellationToken) =>
        RiskReferences.ChangeRefused(project)
        ?? (RiskWorkflow.IsOpen(risk.Status) ? null : AdministrationError.Conflict(RiskErrorCodes.Closed))
        ?? (action is null || IsOpen(action.Status) ? null : AdministrationError.Conflict(RiskErrorCodes.ActionNotEditable))
        ?? (newOwnerUserId is { } owner ? await references.OwnerRefusedAsync(project, owner, cancellationToken).ConfigureAwait(false) : null);

    /// <summary>The action, tracked, and its risk, tracked, and project, if the caller holds <paramref name="permissionCode"/> on the project.</summary>
    private async Task<LoadedAction> LoadAsync(Guid callerId, string permissionCode, Guid actionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        RiskTreatmentAction? action = await repository.FindActionAsync(actionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (action is null)
        {
            return new LoadedAction(null, null, null, AdministrationError.NotFound);
        }

        LoadedRisk risk = await gate.LoadRiskAsync(callerId, permissionCode, action.RiskId, null, cancellationToken).ConfigureAwait(false);
        return new LoadedAction(risk.Project, risk.Risk, action, risk.Error);
    }

    private async Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> SaveAsync(IRiskWork work, RiskTreatmentAction action, CancellationToken cancellationToken) =>
        await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false)
            ? new Versioned<RiskTreatmentActionDetail>(RiskViews.ToDetail(action), repository.RowVersionOf(action))
            : AdministrationError.PreconditionFailed;

    private sealed record LoadedAction(ProjectFacts? Project, RiskEntity? Risk, RiskTreatmentAction? Action, AdministrationError? Error);
}
