using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.AuditActivity.Contracts;

/// <summary>
/// ADM-050 and ADM-051 (FG-06 §8): formal audit, read by category, scope and sensitivity (AUD-CC-02 to -05). No operation changes
/// an event: there is none to call (BR-AUD-003).
/// </summary>
public interface IAuditEventService
{
    public Task<AuditAccessView> GetAccessAsync(Guid callerId, CancellationToken cancellationToken);

    /// <summary>The events the caller may read that match, newest first. Naming a category the caller may not read is 403.</summary>
    public Task<AdministrationResult<AuditEventPage>> SearchAsync(Guid callerId, AuditEventQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>One event. An event outside the caller's categories, scope or clearance is 404, as an unknown id is, and the refusal is audited.</summary>
    public Task<AdministrationResult<AuditEventDetail>> GetAsync(Guid callerId, Guid auditEventId, CancellationToken cancellationToken);
}
