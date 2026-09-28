using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>The stable identities of <c>master_data_config</c>: catalogues, their items, and KPI definitions.</summary>
public interface IMasterDataRepository
{
    public Task<IReadOnlyList<MasterDataCatalogueDetail>> ListCataloguesAsync(CancellationToken cancellationToken);

    public Task<Versioned<MasterDataCatalogueDetail>?> FindCatalogueAsync(Guid catalogueId, CancellationToken cancellationToken);

    public Task<MasterDataCatalogue?> FindCatalogueForUpdateAsync(Guid catalogueId, uint expectedVersion, CancellationToken cancellationToken);

    public Task<MasterDataCatalogue?> FindCatalogueByCodeAsync(string code, CancellationToken cancellationToken);

    public Task<MasterDataItemPage> ListItemsAsync(MasterDataItemQuery query, CancellationToken cancellationToken);

    public Task<Versioned<MasterDataItemDetail>?> FindItemAsync(Guid itemId, CancellationToken cancellationToken);

    public Task<MasterDataItem?> FindItemForUpdateAsync(Guid itemId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Every item's parent within one catalogue, for the cycle check.</summary>
    public Task<IReadOnlyDictionary<Guid, Guid?>> GetParentsAsync(Guid catalogueId, CancellationToken cancellationToken);

    /// <summary>The catalogue and state of each item named; an id with no item is absent from the answer.</summary>
    public Task<IReadOnlyDictionary<Guid, ItemFacts>> FindItemFactsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken);

    /// <summary>The PUBLISHED items of a catalogue in display order.</summary>
    public Task<IReadOnlyList<MasterDataItemReference>> ListPublishedItemsAsync(string catalogueCode, CancellationToken cancellationToken);

    public void AddItem(MasterDataItem item);

    public Task<KpiDefinitionPage> ListKpiDefinitionsAsync(KpiDefinitionQuery query, CancellationToken cancellationToken);

    public Task<Versioned<KpiDefinitionDetail>?> FindKpiDefinitionAsync(Guid kpiDefinitionId, CancellationToken cancellationToken);

    public Task<KpiDefinition?> FindKpiDefinitionForUpdateAsync(Guid kpiDefinitionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The state of each KPI definition named; an id with no definition is absent from the answer.</summary>
    public Task<IReadOnlyDictionary<Guid, GovernedLifecycleState>> FindKpiDefinitionStatesAsync(
        IReadOnlyCollection<Guid> kpiDefinitionIds, CancellationToken cancellationToken);

    public void AddKpiDefinition(KpiDefinition definition);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
