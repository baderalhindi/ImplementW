using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>The controlled values a document names (E-U2), and links assembled with their evidence and pinned versions.</summary>
internal sealed class DocumentReferences(IMasterDataResolver masterData, IDocumentRepository repository)
{
    /// <summary>The first of the document type and classification that is not a PUBLISHED item of its catalogue.</summary>
    public async Task<FieldIssue?> MetadataIssueAsync(Guid documentTypeItemId, Guid classificationItemId, CancellationToken cancellationToken) =>
        await ItemIssueAsync(MasterDataCatalogueCodes.DocumentType, documentTypeItemId, "documentTypeItemId", cancellationToken).ConfigureAwait(false)
        ?? await ItemIssueAsync(MasterDataCatalogueCodes.DataClassification, classificationItemId, "dataClassificationItemId", cancellationToken).ConfigureAwait(false);

    public Task<FieldIssue?> EvidenceTypeIssueAsync(Guid evidenceTypeItemId, CancellationToken cancellationToken) =>
        ItemIssueAsync(MasterDataCatalogueCodes.EvidenceType, evidenceTypeItemId, "evidenceTypeItemId", cancellationToken);

    public async Task<IReadOnlyList<BusinessLinkDetail>> DetailsAsync(IReadOnlyList<BusinessLink> links, CancellationToken cancellationToken)
    {
        IReadOnlyList<EvidenceReference> evidence = await repository.GetEvidenceAsync([.. links.Select(l => l.Id)], cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, DocumentVersion> versions = await repository.GetVersionsByIdAsync([.. evidence.Select(e => e.DocumentVersionId).Distinct()], cancellationToken).ConfigureAwait(false);
        ILookup<Guid, EvidenceReference> byLink = evidence.ToLookup(e => e.BusinessLinkId);
        return [.. links.Select(l => DocumentMapping.ToDetail(l, byLink[l.Id].Select(e => DocumentMapping.ToDetail(e, l, versions[e.DocumentVersionId]))))];
    }

    private async Task<FieldIssue?> ItemIssueAsync(string catalogueCode, Guid itemId, string field, CancellationToken cancellationToken)
    {
        try
        {
            await masterData.RequirePublishedItemAsync(catalogueCode, itemId, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (ConfigurationMissingException)
        {
            return new FieldIssue(field, FieldIssue.NotFound);
        }
    }
}
