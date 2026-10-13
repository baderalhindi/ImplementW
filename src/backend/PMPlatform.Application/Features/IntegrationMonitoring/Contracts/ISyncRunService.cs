using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

/// <summary>ADM-047's runs (FG-05 US-INT-ADM-008), and the reconciliation an operator requests (US-INT-OPS-047).</summary>
public interface ISyncRunService
{
    public Task<SyncRunPage> SearchAsync(SyncRunQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<SyncRunView>> GetAsync(Guid syncRunId, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the integration's reconciliation, keyed by <paramref name="requestKey"/>: the same key again answers the lineage it began
    /// and runs nothing twice. The run reports mismatches as an alert and changes no authoritative data (BR-INT-006).
    /// </summary>
    public Task<AdministrationResult<InvocationDetail>> ReconcileAsync(Guid actorId, Guid integrationId, string requestKey, CancellationToken cancellationToken);
}
