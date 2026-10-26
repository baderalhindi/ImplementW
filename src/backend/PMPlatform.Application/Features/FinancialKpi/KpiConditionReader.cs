using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>Edge 29: each ACTIVE assignment's RAG, as its latest published measurement stored it.</summary>
internal sealed class KpiConditionReader(IFinancialKpiRepository repository) : IKpiConditionReader
{
    public async Task<IReadOnlyList<KpiCondition>> ListAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        IReadOnlyList<KpiAssignment> assignments = await repository.ListActiveAssignmentsAsync(projectIds, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, KpiMeasurement> latest = (await repository.ListLatestPublishedMeasurementsAsync([.. assignments.Select(a => a.Id)], cancellationToken).ConfigureAwait(false))
            .ToDictionary(m => m.KpiAssignmentId);
        return [.. assignments.Select(a => latest.TryGetValue(a.Id, out KpiMeasurement? m)
            ? new KpiCondition(a.ProjectId, a.Id, a.KpiDefinitionId, m.RagStatus, m.ValueStatus, m.AsOfDate, m.PublishedAt)
            : new KpiCondition(a.ProjectId, a.Id, a.KpiDefinitionId, null, null, null, null))];
    }
}
