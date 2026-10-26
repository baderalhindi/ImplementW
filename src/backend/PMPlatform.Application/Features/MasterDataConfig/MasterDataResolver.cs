using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>E-U2 for master data (TASK-034): only a PUBLISHED item of the named catalogue may be used in a new record.</summary>
internal sealed class MasterDataResolver(IMasterDataRepository masterData) : IMasterDataResolver
{
    public async Task<MasterDataItemReference> RequirePublishedItemAsync(string catalogueCode, Guid itemId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(catalogueCode);

        return (await masterData.FindItemAsync(itemId, cancellationToken).ConfigureAwait(false))?.Value is
        { Governance.LifecycleState: GovernedLifecycleState.Published } item && item.CatalogueCode == catalogueCode
            ? new MasterDataItemReference(item.Id, item.CatalogueCode, item.Code, item.Label, item.ParentItemId, item.SortOrder)
            : throw new ConfigurationMissingException(catalogueCode, ConfigurationMissingReason.ItemUnavailable, $"item {itemId}");
    }

    public async Task<IReadOnlyList<MasterDataItemReference>> ListPublishedItemsAsync(string catalogueCode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(catalogueCode);

        return await masterData.FindCatalogueByCodeAsync(catalogueCode, cancellationToken).ConfigureAwait(false) is null
            ? throw new ConfigurationMissingException(catalogueCode, ConfigurationMissingReason.UnknownCode, entry: null)
            : await masterData.ListPublishedItemsAsync(catalogueCode, cancellationToken).ConfigureAwait(false);
    }
}
