using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Risk.Contracts;

/// <summary>
/// A risk's way along its state machine (TASK-055): Identified → Assessed → Treatment ⇄ Monitoring → Closed, with a time-bound
/// acceptance that returns the risk for review, and the controlled reopen. Each command is one edge family (R-4), takes the
/// caller's version of the risk when they send one (R-21), and answers with the risk as saved.
/// </summary>
public interface IRiskLifecycleService
{
    /// <summary>
    /// Records a new assessment version, rated by the RISK_MATRIX version in force now, which it pins; IDENTIFIED → ASSESSED on
    /// the first. RISK_ASSESS, internal users only: rating authority is unchanged for entity users (ADR-013).
    /// </summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> AssessAsync(
        Guid callerId, Guid riskId, RiskAssessmentDraft draft, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>ASSESSED or MONITORING → TREATMENT, with a live treatment action and no ACTIVE acceptance. RISK_MANAGE.</summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> StartTreatmentAsync(Guid callerId, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>ASSESSED or TREATMENT → MONITORING. RISK_MANAGE.</summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> MonitorAsync(Guid callerId, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Accepts an assessed risk until a date after today, and moves it to MONITORING; its next review falls due on that date.
    /// RISK_ACCEPT, internal users only (ADR-013).
    /// </summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> AcceptAsync(
        Guid callerId, Guid riskId, RiskAcceptanceDraft draft, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Ends the ACTIVE acceptance before its expiry; the risk → ASSESSED, back for review. RISK_ACCEPT, internal users only.</summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> RevokeAcceptanceAsync(Guid callerId, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>An open risk → CLOSED, with its rationale; an ACTIVE acceptance is revoked with it. RISK_MANAGE.</summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> CloseAsync(
        Guid callerId, Guid riskId, NarrativeText rationale, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>CLOSED → ASSESSED, or IDENTIFIED if never assessed. RISK_REOPEN only: general edit permission never reopens a risk.</summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> ReopenAsync(Guid callerId, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Has WF-07 raise an issue from an open risk that has none yet (edge 15), and records when, in one transaction. RISK_MANAGE.
    /// The risk's status does not change: closing it is the user's decision.
    /// </summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> MaterialiseAsync(
        Guid callerId, Guid riskId, RiskMaterialisationDraft draft, uint? expectedVersion, CancellationToken cancellationToken);
}
