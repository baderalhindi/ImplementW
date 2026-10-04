using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>The KPI catalogue as this module reads it (E-U2): a definition's unit and direction, and whether it may be assigned.</summary>
internal sealed class KpiDefinitions(IKpiDefinitionAdministrationService catalogue)
{
    /// <summary>The definition, or null when there is none.</summary>
    public async Task<KpiDefinitionDetail?> FindAsync(Guid kpiDefinitionId, CancellationToken cancellationToken)
    {
        AdministrationResult<Versioned<KpiDefinitionDetail>> found = await catalogue.GetAsync(kpiDefinitionId, cancellationToken).ConfigureAwait(false);
        return found.Succeeded ? found.Value.Value : null;
    }

    /// <summary>The definitions named, by id; one that does not exist is absent.</summary>
    public async Task<IReadOnlyDictionary<Guid, KpiDefinitionDetail>> FindAllAsync(IEnumerable<Guid> kpiDefinitionIds, CancellationToken cancellationToken)
    {
        Dictionary<Guid, KpiDefinitionDetail> found = [];
        foreach (Guid id in kpiDefinitionIds.Distinct())
        {
            if (await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } definition)
            {
                found[id] = definition;
            }
        }

        return found;
    }

    /// <summary>The definition a measurement of the assignment is rated by; an assignment always names one.</summary>
    public async Task<KpiDefinitionDetail> RequireAsync(Guid kpiDefinitionId, CancellationToken cancellationToken) =>
        await FindAsync(kpiDefinitionId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException($"KPI definition {kpiDefinitionId} does not exist.");
}
