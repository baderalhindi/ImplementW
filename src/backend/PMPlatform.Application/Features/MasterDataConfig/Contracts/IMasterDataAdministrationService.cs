using PMPlatform.Application.Common.Deletion;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// ADM-020–029 master data (TASK-034): the catalogues and their items. An item is never deleted or versioned: it is
/// authored as a DRAFT, validated and published by two other people, and retired; domain rows reference it forever.
/// </summary>
public interface IMasterDataAdministrationService
{
    public Task<IReadOnlyList<MasterDataCatalogueDetail>> ListCataloguesAsync(CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<MasterDataCatalogueDetail>>> GetCatalogueAsync(Guid catalogueId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<MasterDataCatalogueDetail>>> RenameCatalogueAsync(
        Guid actorId, Guid catalogueId, BilingualLabel name, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<MasterDataItemPage>> ListItemsAsync(MasterDataItemQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<MasterDataItemDetail>>> GetItemAsync(Guid itemId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<MasterDataItemDetail>>> CreateItemAsync(Guid actorId, MasterDataItemDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<MasterDataItemDetail>>> UpdateItemAsync(
        Guid actorId, Guid itemId, MasterDataItemChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<MasterDataItemDetail>>> ValidateItemAsync(Guid actorId, Guid itemId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<MasterDataItemDetail>>> PublishItemAsync(Guid actorId, Guid itemId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<MasterDataItemDetail>>> RetireItemAsync(Guid actorId, Guid itemId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an item, in any state (the client asked for no "retire"): it is cleared from every record that names it optionally, then erased. A record
    /// that cannot exist without it, a closed project, a published configuration or the audit trail keeps it, and then nothing is deleted and the
    /// answer lists those uses. A shipped item is never deleted.
    /// </summary>
    public Task<AdministrationResult<DeletionOutcome>> RemoveItemAsync(Guid actorId, Guid itemId, uint? expectedVersion, CancellationToken cancellationToken);
}
