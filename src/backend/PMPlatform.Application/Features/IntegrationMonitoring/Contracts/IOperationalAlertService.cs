using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

/// <summary>ADM-047's operational alerts and their handling (FG-05 §10.2). Handling an alert never changes the integration's health.</summary>
public interface IOperationalAlertService
{
    public Task<OperationalAlertPage> SearchAsync(OperationalAlertQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<OperationalAlertView>>> GetAsync(Guid alertId, CancellationToken cancellationToken);

    /// <summary>OPEN → ACKNOWLEDGED, recording who and when; it does not claim the integration recovered (BR-INT-038).</summary>
    public Task<AdministrationResult<Versioned<OperationalAlertView>>> AcknowledgeAsync(Guid actorId, Guid alertId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>OPEN or ACKNOWLEDGED → RESOLVED, with the operator's note.</summary>
    public Task<AdministrationResult<Versioned<OperationalAlertView>>> ResolveAsync(Guid actorId, Guid alertId, NarrativeText note, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>OPEN or ACKNOWLEDGED → DISMISSED, with the operator's note: the condition was not real.</summary>
    public Task<AdministrationResult<Versioned<OperationalAlertView>>> DismissAsync(Guid actorId, Guid alertId, NarrativeText note, uint? expectedVersion, CancellationToken cancellationToken);
}
