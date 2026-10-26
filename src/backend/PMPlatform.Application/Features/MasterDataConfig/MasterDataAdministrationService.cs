using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Governance;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// ADM-020–029 (TASK-034). Master data is platform reference data with no record scope, so the endpoint's permission
/// is the whole access check. An item's catalogue and code never change; its parent is in the same catalogue, only
/// where the catalogue allows a hierarchy, and never below itself. Every change is saved with its CONFIGURATION_CHANGE
/// audit event (TASK-033).
/// </summary>
internal sealed partial class MasterDataAdministrationService(
    IMasterDataRepository masterData,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<MasterDataAdministrationService> logger) : IMasterDataAdministrationService
{
    public Task<IReadOnlyList<MasterDataCatalogueDetail>> ListCataloguesAsync(CancellationToken cancellationToken) =>
        masterData.ListCataloguesAsync(cancellationToken);

    public async Task<AdministrationResult<Versioned<MasterDataCatalogueDetail>>> GetCatalogueAsync(Guid catalogueId, CancellationToken cancellationToken) =>
        await masterData.FindCatalogueAsync(catalogueId, cancellationToken).ConfigureAwait(false) is { } catalogue ? catalogue : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<MasterDataCatalogueDetail>>> RenameCatalogueAsync(
        Guid actorId, Guid catalogueId, BilingualLabel name, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        MasterDataCatalogue? catalogue = await masterData.FindCatalogueForUpdateAsync(catalogueId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (catalogue is null)
        {
            return AdministrationError.NotFound;
        }

        AuditAttribute?[] changed = [AuditAttribute.Change("name_ar", catalogue.Name.Ar, name.Ar), AuditAttribute.Change("name_en", catalogue.Name.En, name.En)];
        catalogue.Name = name;
        Stamp(catalogue, actorId);
        audit.Stage(ConfigurationAudit.Entry(MasterDataConfigAuditEvents.CatalogueRenamed, actorId, nameof(MasterDataCatalogue), catalogue.Id, changed));
        if ((await masterData.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogChanged(logger, actorId, "renamed", nameof(MasterDataCatalogue), catalogue.Id);
        return await masterData.FindCatalogueAsync(catalogue.Id, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Catalogue {catalogue.Id} was saved and cannot be read back.");
    }

    public async Task<AdministrationResult<MasterDataItemPage>> ListItemsAsync(MasterDataItemQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await masterData.ListItemsAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<MasterDataItemDetail>>> GetItemAsync(Guid itemId, CancellationToken cancellationToken) =>
        await masterData.FindItemAsync(itemId, cancellationToken).ConfigureAwait(false) is { } item ? item : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<MasterDataItemDetail>>> CreateItemAsync(Guid actorId, MasterDataItemDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if ((await masterData.FindCatalogueAsync(draft.CatalogueId, cancellationToken).ConfigureAwait(false))?.Value is not { } catalogue)
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.ReferenceInvalid, new FieldIssue("catalogueId", FieldIssue.NotFound));
        }

        if (await CheckParentAsync(catalogue, itemId: null, draft.ParentItemId, cancellationToken).ConfigureAwait(false) is { } parentIssue)
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.ReferenceInvalid, parentIssue);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        MasterDataItem item = new()
        {
            Id = Guid.CreateVersion7(now),
            CatalogueId = catalogue.Id,
            Code = draft.Code,
            Label = draft.Label,
            Description = draft.Description,
            ParentItemId = draft.ParentItemId,
            SortOrder = draft.SortOrder,
            LifecycleState = GovernedLifecycleState.Draft,
            CreatedAt = now,
            CreatedBy = actorId,
        };
        masterData.AddItem(item);
        return await SaveItemAsync(actorId, item, "created", MasterDataConfigAuditEvents.ItemCreated,
            [
                AuditAttribute.Change("catalogue_id", null, item.CatalogueId),
                AuditAttribute.Change("code", null, item.Code),
                AuditAttribute.Change("label_ar", null, item.Label.Ar),
                AuditAttribute.Change("label_en", null, item.Label.En),
                AuditAttribute.Change("description_ar", null, item.Description?.Ar),
                AuditAttribute.Change("description_en", null, item.Description?.En),
                AuditAttribute.Change("parent_item_id", null, item.ParentItemId),
                AuditAttribute.Change("sort_order", null, item.SortOrder),
                AuditAttribute.Change("lifecycle_state", null, item.LifecycleState),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A DRAFT is edited by its author only. A PUBLISHED item takes a label, description or order correction from any
    /// administrator as an audited edit (ERD §5.3), but its place in the hierarchy is fixed. VALIDATED waits for its publisher.
    /// </summary>
    public async Task<AdministrationResult<Versioned<MasterDataItemDetail>>> UpdateItemAsync(
        Guid actorId, Guid itemId, MasterDataItemChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        MasterDataItem? item = await masterData.FindItemForUpdateAsync(itemId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return AdministrationError.NotFound;
        }

        if (item.LifecycleState == GovernedLifecycleState.Published)
        {
            if (changes.ParentItemId != item.ParentItemId)
            {
                return AdministrationError.Rule(MasterDataConfigErrorCodes.PublishedImmutable, new FieldIssue("parentItemId", FieldIssue.NotAllowed));
            }
        }
        else if (GovernedRefusal.Of(GovernedLifecycle.CheckDraftEdit(item, actorId)) is { } refused)
        {
            return refused;
        }
        else if (changes.ParentItemId != item.ParentItemId)
        {
            MasterDataCatalogueDetail catalogue = (await masterData.FindCatalogueAsync(item.CatalogueId, cancellationToken).ConfigureAwait(false))!.Value;
            if (await CheckParentAsync(catalogue, item.Id, changes.ParentItemId, cancellationToken).ConfigureAwait(false) is { } parentIssue)
            {
                return AdministrationError.Rule(MasterDataConfigErrorCodes.ReferenceInvalid, parentIssue);
            }
        }

        AuditAttribute?[] changed =
        [
            AuditAttribute.Change("label_ar", item.Label.Ar, changes.Label.Ar),
            AuditAttribute.Change("label_en", item.Label.En, changes.Label.En),
            AuditAttribute.Change("description_ar", item.Description?.Ar, changes.Description?.Ar),
            AuditAttribute.Change("description_en", item.Description?.En, changes.Description?.En),
            AuditAttribute.Change("parent_item_id", item.ParentItemId, changes.ParentItemId),
            AuditAttribute.Change("sort_order", item.SortOrder, changes.SortOrder),
        ];
        item.Label = changes.Label;
        item.Description = changes.Description;
        item.ParentItemId = changes.ParentItemId;
        item.SortOrder = changes.SortOrder;
        return await SaveItemAsync(actorId, item, "updated", MasterDataConfigAuditEvents.ItemUpdated, changed, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<MasterDataItemDetail>>> ValidateItemAsync(
        Guid actorId, Guid itemId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        MasterDataItem? item = await masterData.FindItemForUpdateAsync(itemId, expectedVersion, cancellationToken).ConfigureAwait(false);
        return item is null
            ? (AdministrationResult<Versioned<MasterDataItemDetail>>)AdministrationError.NotFound
            : GovernedRefusal.Of(GovernedLifecycle.Validate(item, actorId, timeProvider.GetUtcNow())) is { } refused
            ? refused
            : await SaveItemAsync(actorId, item, "validated", MasterDataConfigAuditEvents.ItemValidated,
                [AuditAttribute.Change("lifecycle_state", GovernedLifecycleState.Draft, item.LifecycleState)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A child is published only under a PUBLISHED parent, so a form never offers a value whose parent it cannot show.</summary>
    public async Task<AdministrationResult<Versioned<MasterDataItemDetail>>> PublishItemAsync(
        Guid actorId, Guid itemId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        MasterDataItem? item = await masterData.FindItemForUpdateAsync(itemId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return AdministrationError.NotFound;
        }

        if (GovernedRefusal.Of(GovernedLifecycle.CheckPublish(item, actorId)) is { } refused)
        {
            return refused;
        }

        if (item.ParentItemId is { } parentId
            && (await masterData.FindItemFactsAsync([parentId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(parentId)
                is not { LifecycleState: GovernedLifecycleState.Published })
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.ReferenceInvalid, new FieldIssue("parentItemId", FieldIssue.Inactive));
        }

        GovernedLifecycle.Publish(item, actorId, timeProvider.GetUtcNow());
        return await SaveItemAsync(actorId, item, "published", MasterDataConfigAuditEvents.ItemPublished,
            [AuditAttribute.Change("lifecycle_state", GovernedLifecycleState.Validated, item.LifecycleState)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Rows that reference a retired item keep it; only new use stops. A shipped item is never retired.</summary>
    public async Task<AdministrationResult<Versioned<MasterDataItemDetail>>> RetireItemAsync(
        Guid actorId, Guid itemId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        MasterDataItem? item = await masterData.FindItemForUpdateAsync(itemId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return AdministrationError.NotFound;
        }

        if (item.IsSystem)
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.SystemRow);
        }

        GovernedLifecycleState before = item.LifecycleState;
        return GovernedRefusal.Of(GovernedLifecycle.Retire(item, timeProvider.GetUtcNow())) is { } refused
            ? refused
            : await SaveItemAsync(actorId, item, "retired", MasterDataConfigAuditEvents.ItemRetired,
                [AuditAttribute.Change("lifecycle_state", before, item.LifecycleState)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="parentId"/> is <paramref name="itemId"/> or one of its descendants, or its ancestry already loops.</summary>
    internal static bool IsOwnAncestor(Guid? itemId, Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        HashSet<Guid> seen = [];
        for (Guid? current = parentId; current is { } id; current = parents.GetValueOrDefault(id))
        {
            if (id == itemId || !seen.Add(id))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<FieldIssue?> CheckParentAsync(MasterDataCatalogueDetail catalogue, Guid? itemId, Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is not { } id)
        {
            return null;
        }

        if (!catalogue.AllowsHierarchy)
        {
            return new FieldIssue("parentItemId", FieldIssue.NotAllowed);
        }

        ItemFacts? parent = (await masterData.FindItemFactsAsync([id], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(id);
        return parent is null || parent.CatalogueCode != catalogue.Code ? new FieldIssue("parentItemId", FieldIssue.NotFound)
            : parent.LifecycleState == GovernedLifecycleState.Retired ? new FieldIssue("parentItemId", FieldIssue.Inactive)
            : IsOwnAncestor(itemId, id, await masterData.GetParentsAsync(catalogue.Id, cancellationToken).ConfigureAwait(false)) ? new FieldIssue("parentItemId", FieldIssue.NotAllowed)
            : null;
    }

    private void Stamp(AuditedEntity row, Guid actorId)
    {
        row.UpdatedAt = timeProvider.GetUtcNow();
        row.UpdatedBy = actorId;
    }

    private async Task<AdministrationResult<Versioned<MasterDataItemDetail>>> SaveItemAsync(
        Guid actorId, MasterDataItem item, string change, string eventType, AuditAttribute?[] attributes, CancellationToken cancellationToken)
    {
        Stamp(item, actorId);
        audit.Stage(ConfigurationAudit.Entry(eventType, actorId, nameof(MasterDataItem), item.Id, attributes));
        if ((await masterData.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogChanged(logger, actorId, change, nameof(MasterDataItem), item.Id);
        return await masterData.FindItemAsync(item.Id, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Item {item.Id} was saved and cannot be read back.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Master data administration: {ActorId} {Change} {SubjectType} {SubjectId}.")]
    private static partial void LogChanged(ILogger logger, Guid actorId, string change, string subjectType, Guid subjectId);
}
