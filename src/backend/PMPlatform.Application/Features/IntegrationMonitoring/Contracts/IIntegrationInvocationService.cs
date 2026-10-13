using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

/// <summary>ADM-048 Integration Logs (TASK-075): attempts searched by correlation, lineage and business reference, and the manual retry.</summary>
public interface IIntegrationInvocationService
{
    public Task<InvocationPage> SearchAsync(InvocationQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<InvocationDetail>> GetAsync(Guid invocationId, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the lineage's next attempt now, under the lineage's own correlation id and key (FG-05 §7.1): allowed only from its latest
    /// attempt, only when that attempt is dead-lettered or waiting for a retry, and only for an operation FG-05 runs itself.
    /// </summary>
    public Task<AdministrationResult<InvocationDetail>> RetryAsync(Guid actorId, Guid invocationId, CancellationToken cancellationToken);
}
