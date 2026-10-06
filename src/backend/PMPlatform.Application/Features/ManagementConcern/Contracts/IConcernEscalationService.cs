using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>
/// Escalations of concerns (TASK-057): raised by internal users only (ADR-013), addressed to the role WORKFLOW_POLICY routes them to,
/// and published to WF-15 as one NotificationIntent each, keyed by the escalation's id. A retry carrying the same
/// <c>Idempotency-Key</c> finds the escalation it raised and publishes nothing. An escalation never changes its concern's status.
/// </summary>
public interface IConcernEscalationService
{
    /// <summary>Escalates an open concern. CONCERN_ESCALATE, internal users.</summary>
    public Task<AdministrationResult<EscalationOutcome>> EscalateAsync(Guid callerId, ConcernEscalationDraft draft, CancellationToken cancellationToken);

    /// <summary>OPEN → RESOLVED, with the direction given. CONCERN_ESCALATION_RESOLVE held through the role it is addressed to, internal users.</summary>
    public Task<AdministrationResult<ConcernEscalationDetail>> ResolveAsync(Guid callerId, Guid escalationId, NarrativeText resolution, CancellationToken cancellationToken);

    /// <summary>OPEN → WITHDRAWN, by its escalator.</summary>
    public Task<AdministrationResult<ConcernEscalationDetail>> WithdrawAsync(Guid callerId, Guid escalationId, CancellationToken cancellationToken);

    public Task<AdministrationResult<ConcernEscalationDetail>> GetAsync(Guid callerId, Guid escalationId, CancellationToken cancellationToken);

    /// <summary>The concern's escalations, newest first; empty for a concern the caller may not see.</summary>
    public Task<ConcernEscalationPage> ListAsync(Guid callerId, Guid concernId, PageRequest page, CancellationToken cancellationToken);
}
