using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

/// <summary>ADM-040 Integrations Overview and ADM-046 Integration Detail (TASK-075), and MOD-101's enable and disable (TASK-076).</summary>
public interface IIntegrationCatalogue
{
    /// <summary>Every integration with its instances, by code. The API adds the environment it runs in (<see cref="IntegrationCatalogueView"/>).</summary>
    public Task<IReadOnlyList<IntegrationSummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The integration, its instances, and each instance's measures from <paramref name="windowStart"/> to <paramref name="windowEnd"/>; without
    /// them, over the policy's metrics window up to now.
    /// </summary>
    public Task<AdministrationResult<Versioned<IntegrationDetail>>> GetAsync(Guid integrationId, DateTimeOffset? windowStart, DateTimeOffset? windowEnd, CancellationToken cancellationToken);

    /// <summary>DISABLED → ACTIVE. Monitoring activation only: no adapter changes behaviour (BR-INT-008).</summary>
    public Task<AdministrationResult<Versioned<IntegrationDetail>>> EnableAsync(Guid actorId, Guid integrationId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>ACTIVE → DISABLED: telemetry is still recorded, no alert is raised and FG-05 runs none of its operations.</summary>
    public Task<AdministrationResult<Versioned<IntegrationDetail>>> DisableAsync(Guid actorId, Guid integrationId, uint? expectedVersion, CancellationToken cancellationToken);
}
