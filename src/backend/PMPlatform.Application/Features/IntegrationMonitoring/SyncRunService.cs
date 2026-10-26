using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>ADM-047's runs and the operator's reconciliation request (TASK-075). A reconciliation never repairs what it finds (BR-INT-006).</summary>
internal sealed class SyncRunService(
    IIntegrationMonitoringRepository repository, IntegrationInvocationEngine engine, IntegrationInvocationService invocations) : ISyncRunService
{
    public async Task<SyncRunPage> SearchAsync(SyncRunQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        (IReadOnlyList<SyncRun> items, int total) = await repository.SearchSyncRunsAsync(query, page, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, string> codes = await repository.GetIntegrationCodesAsync(cancellationToken).ConfigureAwait(false);
        return new SyncRunPage([.. items.Select(r => IntegrationMonitoringMapping.ToView(r, codes))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<SyncRunView>> GetAsync(Guid syncRunId, CancellationToken cancellationToken) =>
        await repository.FindSyncRunAsync(syncRunId, cancellationToken).ConfigureAwait(false) is { } run
            ? IntegrationMonitoringMapping.ToView(run, await repository.GetIntegrationCodesAsync(cancellationToken).ConfigureAwait(false))
            : AdministrationError.NotFound;

    public async Task<AdministrationResult<InvocationDetail>> ReconcileAsync(Guid actorId, Guid integrationId, string requestKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
        if (await repository.FindDefinitionAsync(integrationId, null, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (engine.RunOperationFor(definition.Code, SyncRunType.Reconciliation) is not { } operation)
        {
            return AdministrationError.Rule(IntegrationMonitoringErrorCodes.ReconciliationNotAvailable);
        }

        if (definition.Status != IntegrationDefinitionStatus.Active)
        {
            return AdministrationError.Rule(IntegrationMonitoringErrorCodes.IntegrationDisabled);
        }

        if (!operation.IsAvailable)
        {
            return AdministrationError.Unavailable;
        }

        // The lineage key is the caller's request key, under the caller's own name: a repeated request answers the run it began.
        Invocation attempt = await engine.StartAsync(
            definition, operation, $"{actorId:N}:{requestKey}", started => IntegrationMonitoringAudit.ReconciliationRequested(actorId, started, definition.Code),
            cancellationToken).ConfigureAwait(false);
        return await invocations.DetailAsync(attempt, cancellationToken).ConfigureAwait(false);
    }
}
