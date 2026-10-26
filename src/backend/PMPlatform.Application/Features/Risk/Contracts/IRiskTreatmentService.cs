using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Risk.Contracts;

/// <summary>
/// A risk's treatment and mitigation actions (TASK-055): PLANNED → IN_PROGRESS → COMPLETED, or CANCELLED while open. RISK_VIEW to
/// read them, RISK_MANAGE to change them, while the risk is open.
/// </summary>
public interface IRiskTreatmentService
{
    public Task<RiskTreatmentActionPage> ListActionsAsync(Guid callerId, Guid riskId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> GetActionAsync(Guid callerId, Guid actionId, CancellationToken cancellationToken);

    /// <summary>Adds an action, PLANNED.</summary>
    public Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> CreateActionAsync(Guid callerId, RiskTreatmentActionDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces an open action's plan. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> UpdateActionAsync(
        Guid callerId, Guid actionId, RiskTreatmentActionChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>PLANNED → IN_PROGRESS.</summary>
    public Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> StartActionAsync(Guid callerId, Guid actionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>IN_PROGRESS → COMPLETED.</summary>
    public Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> CompleteActionAsync(Guid callerId, Guid actionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>PLANNED or IN_PROGRESS → CANCELLED, final: RETAIN has no DELETE (R-5).</summary>
    public Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>> CancelActionAsync(Guid callerId, Guid actionId, uint? expectedVersion, CancellationToken cancellationToken);
}
