using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>ADM-048 (TASK-075): attempts and their lineages, and the operator's retry of a lineage FG-05 runs.</summary>
internal sealed class IntegrationInvocationService(IIntegrationMonitoringRepository repository, IntegrationInvocationEngine engine) : IIntegrationInvocationService
{
    public async Task<InvocationPage> SearchAsync(InvocationQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        (IReadOnlyList<Invocation> items, int total) = await repository.SearchInvocationsAsync(query, page, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, string> codes = await repository.GetIntegrationCodesAsync(cancellationToken).ConfigureAwait(false);
        return new InvocationPage([.. items.Select(i => IntegrationMonitoringMapping.ToSummary(i, codes))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<InvocationDetail>> GetAsync(Guid invocationId, CancellationToken cancellationToken) =>
        await repository.FindInvocationAsync(invocationId, cancellationToken).ConfigureAwait(false) is { } invocation
            ? await DetailAsync(invocation, cancellationToken).ConfigureAwait(false)
            : AdministrationError.NotFound;

    public async Task<AdministrationResult<InvocationDetail>> RetryAsync(Guid actorId, Guid invocationId, CancellationToken cancellationToken)
    {
        (Invocation? attempt, AdministrationRefusal? refusal) = await engine.RetryNowAsync(actorId, invocationId, cancellationToken).ConfigureAwait(false);
        return (attempt, refusal) switch
        {
            (null, _) => AdministrationError.NotFound,
            (_, { Conflict: true } conflict) => AdministrationError.Conflict(conflict.Code),
            (_, { } rule) => AdministrationError.Rule(rule.Code),
            ({ } retried, null) => await DetailAsync(retried, cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>The attempt, its lineage oldest first, and whether FG-05 would retry the lineage now.</summary>
    internal async Task<InvocationDetail> DetailAsync(Invocation invocation, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, string> codes = await repository.GetIntegrationCodesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Invocation> lineage = invocation.IdempotencyKey is { } key
            ? await repository.GetLineageAsync(invocation.IntegrationInstanceId, invocation.OperationCode, key, cancellationToken).ConfigureAwait(false)
            : [invocation];
        Invocation current = lineage.Single(i => i.Id == invocation.Id);
        IntegrationInstance instance = (await repository.FindInstanceAsync(invocation.IntegrationInstanceId, cancellationToken).ConfigureAwait(false))!;
        IntegrationDefinition definition = (await repository.FindDefinitionAsync(instance.IntegrationDefinitionId, null, cancellationToken).ConfigureAwait(false))!;
        bool retryPermitted = engine.RetryRefusal(definition, current, lineage[^1]) is null;
        return new InvocationDetail(
            IntegrationMonitoringMapping.ToSummary(current, codes), [.. lineage.Select(i => IntegrationMonitoringMapping.ToSummary(i, codes))], retryPermitted);
    }
}
