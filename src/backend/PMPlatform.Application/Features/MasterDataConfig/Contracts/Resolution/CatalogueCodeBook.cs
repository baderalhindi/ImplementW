using PMPlatform.Application.Common.DataImport;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

/// <summary>
/// The PUBLISHED items of the catalogues a migration workbook names by code (TASK-102), each catalogue read once per import: a workbook
/// gives a classification or a region as the code a person reads in the template, never as an id. Only what a form offers resolves.
/// </summary>
public sealed class CatalogueCodeBook(IMasterDataResolver resolver)
{
    private readonly Dictionary<string, IReadOnlyList<MasterDataItemReference>> _catalogues = new(StringComparer.Ordinal);

    /// <summary>The PUBLISHED item of <paramref name="catalogueCode"/> with this code; null when there is none.</summary>
    public async Task<MasterDataItemReference?> FindAsync(string catalogueCode, string itemCode, CancellationToken cancellationToken) =>
        (await ItemsAsync(catalogueCode, cancellationToken).ConfigureAwait(false)).FirstOrDefault(i => i.Code == itemCode);

    /// <summary>The catalogue's PUBLISHED items as the template's Reference values sheet lists them.</summary>
    public async Task<IReadOnlyList<ImportReferenceValue>> ReferenceValuesAsync(string catalogueCode, CancellationToken cancellationToken) =>
        [.. (await ItemsAsync(catalogueCode, cancellationToken).ConfigureAwait(false)).Select(i => new ImportReferenceValue(i.Code, i.Label.Ar, i.Label.En))];

    private async Task<IReadOnlyList<MasterDataItemReference>> ItemsAsync(string catalogueCode, CancellationToken cancellationToken)
    {
        if (!_catalogues.TryGetValue(catalogueCode, out IReadOnlyList<MasterDataItemReference>? items))
        {
            _catalogues[catalogueCode] = items = await resolver.ListPublishedItemsAsync(catalogueCode, cancellationToken).ConfigureAwait(false);
        }

        return items;
    }
}
