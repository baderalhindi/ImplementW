using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.MasterDataConfig;

/// <summary>ADM-020–029 and the KPI catalogue over <c>master_data_config</c> (TASK-034).</summary>
internal sealed class MasterDataRepository(PMPlatformDbContext context) : IMasterDataRepository
{
    public async Task<IReadOnlyList<MasterDataCatalogueDetail>> ListCataloguesAsync(CancellationToken cancellationToken) =>
        await context.Set<MasterDataCatalogue>().AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new MasterDataCatalogueDetail(c.Id, c.Code, c.Name, c.AllowsHierarchy, c.IsSystem, c.UpdatedAt, c.UpdatedBy, c.ParentCatalogueId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<Versioned<MasterDataCatalogueDetail>?> FindCatalogueAsync(Guid catalogueId, CancellationToken cancellationToken)
    {
        var row = await context.Set<MasterDataCatalogue>().AsNoTracking()
            .Where(c => c.Id == catalogueId)
            .Select(c => new { Catalogue = c, Version = EF.Property<uint>(c, EntityTypeBuilderExtensions.RowVersion) })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null
            ? null
            : new Versioned<MasterDataCatalogueDetail>(
                new MasterDataCatalogueDetail(
                    row.Catalogue.Id, row.Catalogue.Code, row.Catalogue.Name, row.Catalogue.AllowsHierarchy, row.Catalogue.IsSystem,
                    row.Catalogue.UpdatedAt, row.Catalogue.UpdatedBy, row.Catalogue.ParentCatalogueId),
                row.Version);
    }

    public async Task<MasterDataCatalogue?> FindCatalogueForUpdateAsync(Guid catalogueId, uint expectedVersion, CancellationToken cancellationToken)
    {
        MasterDataCatalogue? catalogue = await context.Set<MasterDataCatalogue>().SingleOrDefaultAsync(c => c.Id == catalogueId, cancellationToken).ConfigureAwait(false);
        if (catalogue is not null)
        {
            AdministrationPersistence.ExpectVersion(context, catalogue, expectedVersion);
        }

        return catalogue;
    }

    public Task<MasterDataCatalogue?> FindCatalogueByCodeAsync(string code, CancellationToken cancellationToken) =>
        context.Set<MasterDataCatalogue>().AsNoTracking().SingleOrDefaultAsync(c => c.Code == code, cancellationToken);

    public async Task<MasterDataItemPage> ListItemsAsync(MasterDataItemQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<MasterDataItem> items = context.Set<MasterDataItem>().AsNoTracking();
        if (query.CatalogueId is { } catalogueId)
        {
            items = items.Where(i => i.CatalogueId == catalogueId);
        }

        if (query.LifecycleStates.Count > 0)
        {
            List<GovernedLifecycleState> states = [.. query.LifecycleStates];
            items = items.Where(i => states.Contains(i.LifecycleState));
        }

        if (query.ParentItemId is { } parentId)
        {
            items = items.Where(i => i.ParentItemId == parentId);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            string pattern = AdministrationPersistence.ContainsPattern(query.Text.Trim());
            items = items.Where(i => EF.Functions.ILike(i.Code, pattern, AdministrationPersistence.LikeEscape) || EF.Functions.ILike(i.Label.Ar, pattern, AdministrationPersistence.LikeEscape) || EF.Functions.ILike(i.Label.En, pattern, AdministrationPersistence.LikeEscape));
        }

        int totalCount = await items.CountAsync(cancellationToken).ConfigureAwait(false);
        List<MasterDataItemSummary> page = await items
            .OrderBy(i => i.CatalogueId).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .Select(i => new MasterDataItemSummary(i.Id, i.CatalogueId, i.Code, i.Label, i.ParentItemId, i.SortOrder, i.LifecycleState, i.IsSystem))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new MasterDataItemPage(page, query.Page.Page, query.Page.PageSize, totalCount);
    }

    public async Task<Versioned<MasterDataItemDetail>?> FindItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var row = await context.Set<MasterDataItem>().AsNoTracking()
            .Where(i => i.Id == itemId)
            .Join(context.Set<MasterDataCatalogue>(), i => i.CatalogueId, c => c.Id, (i, c) => new
            {
                Item = i,
                CatalogueCode = c.Code,
                Version = EF.Property<uint>(i, EntityTypeBuilderExtensions.RowVersion),
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        MasterDataItem item = row.Item;
        return new Versioned<MasterDataItemDetail>(
            new MasterDataItemDetail(
                item.Id, item.CatalogueId, row.CatalogueCode, item.Code, item.Label, item.Description, item.ParentItemId, item.SortOrder, item.IsSystem,
                GovernedRecord.Of(item)),
            row.Version);
    }

    public async Task<MasterDataItem?> FindItemForUpdateAsync(Guid itemId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        MasterDataItem? item = await context.Set<MasterDataItem>().SingleOrDefaultAsync(i => i.Id == itemId, cancellationToken).ConfigureAwait(false);
        if (item is not null)
        {
            AdministrationPersistence.ExpectVersion(context, item, expectedVersion);
        }

        return item;
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetParentsAsync(Guid catalogueId, CancellationToken cancellationToken) =>
        await context.Set<MasterDataItem>().AsNoTracking()
            .Where(i => i.CatalogueId == catalogueId)
            .ToDictionaryAsync(i => i.Id, i => i.ParentItemId, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, ItemFacts>> FindItemFactsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = [.. itemIds];
        return await context.Set<MasterDataItem>().AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Join(context.Set<MasterDataCatalogue>(), i => i.CatalogueId, c => c.Id, (i, c) => new ItemFacts(i.Id, c.Code, i.LifecycleState, c.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MasterDataItemReference>> ListPublishedItemsAsync(string catalogueCode, CancellationToken cancellationToken) =>
        await context.Set<MasterDataItem>().AsNoTracking()
            .Join(context.Set<MasterDataCatalogue>(), i => i.CatalogueId, c => c.Id, (i, c) => new { Item = i, CatalogueCode = c.Code })
            .Where(x => x.CatalogueCode == catalogueCode && x.Item.LifecycleState == GovernedLifecycleState.Published)
            .OrderBy(x => x.Item.SortOrder).ThenBy(x => x.Item.Id)
            .Select(x => new MasterDataItemReference(x.Item.Id, x.CatalogueCode, x.Item.Code, x.Item.Label, x.Item.ParentItemId, x.Item.SortOrder, x.Item.Latitude, x.Item.Longitude))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public void AddItem(MasterDataItem item) => context.Set<MasterDataItem>().Add(item);

    public async Task<int> CountReferencesAsync(Guid itemId, CancellationToken cancellationToken)
    {
        // Every single-column foreign key to master_data_item(id), from the catalog; the identifiers are the database's own, quoted.
        List<ReferenceColumn> columns = await context.Database.SqlQuery<ReferenceColumn>($"""
            SELECT quote_ident(n.nspname) || '.' || quote_ident(t.relname) AS "Table", quote_ident(a.attname) AS "Column"
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
            WHERE c.contype = 'f' AND c.confrelid = 'master_data_config.master_data_item'::regclass AND array_length(c.conkey, 1) = 1
            """).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (columns.Count == 0)
        {
            return 0;
        }

        string union = string.Join(" UNION ALL ", columns.Select(c => $"SELECT count(*) AS n FROM {c.Table} WHERE {c.Column} = @item"));
#pragma warning disable EF1002 // The table and column names come from pg_constraint, quoted by quote_ident; the item is a parameter.
        return await context.Database.SqlQueryRaw<int>(
            $"SELECT coalesce(sum(n), 0)::int AS \"Value\" FROM ({union}) r", new Npgsql.NpgsqlParameter("item", itemId)).SingleAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002
    }

    public void RemoveItem(MasterDataItem item) => context.Set<MasterDataItem>().Remove(item);

    private sealed record ReferenceColumn(string Table, string Column);

    public async Task<KpiDefinitionPage> ListKpiDefinitionsAsync(KpiDefinitionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<KpiDefinition> definitions = context.Set<KpiDefinition>().AsNoTracking();
        if (query.LifecycleStates.Count > 0)
        {
            List<GovernedLifecycleState> states = [.. query.LifecycleStates];
            definitions = definitions.Where(d => states.Contains(d.LifecycleState));
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            string pattern = AdministrationPersistence.ContainsPattern(query.Text.Trim());
            definitions = definitions.Where(d => EF.Functions.ILike(d.Code, pattern, AdministrationPersistence.LikeEscape) || EF.Functions.ILike(d.Name.Ar, pattern, AdministrationPersistence.LikeEscape) || EF.Functions.ILike(d.Name.En, pattern, AdministrationPersistence.LikeEscape));
        }

        int totalCount = await definitions.CountAsync(cancellationToken).ConfigureAwait(false);
        List<KpiDefinitionSummary> page = await definitions
            .OrderBy(d => d.Code).ThenBy(d => d.Id)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .Select(d => new KpiDefinitionSummary(d.Id, d.Code, d.Name, d.UnitItemId, d.Direction, d.LifecycleState))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new KpiDefinitionPage(page, query.Page.Page, query.Page.PageSize, totalCount);
    }

    public async Task<Versioned<KpiDefinitionDetail>?> FindKpiDefinitionAsync(Guid kpiDefinitionId, CancellationToken cancellationToken)
    {
        var row = await context.Set<KpiDefinition>().AsNoTracking()
            .Where(d => d.Id == kpiDefinitionId)
            .Select(d => new { Definition = d, Version = EF.Property<uint>(d, EntityTypeBuilderExtensions.RowVersion) })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        KpiDefinition definition = row.Definition;
        return new Versioned<KpiDefinitionDetail>(
            new KpiDefinitionDetail(
                definition.Id, definition.Code, definition.Name, definition.Description, definition.UnitItemId, definition.Direction, GovernedRecord.Of(definition)),
            row.Version);
    }

    public async Task<KpiDefinition?> FindKpiDefinitionForUpdateAsync(Guid kpiDefinitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        KpiDefinition? definition = await context.Set<KpiDefinition>().SingleOrDefaultAsync(d => d.Id == kpiDefinitionId, cancellationToken).ConfigureAwait(false);
        if (definition is not null)
        {
            AdministrationPersistence.ExpectVersion(context, definition, expectedVersion);
        }

        return definition;
    }

    public async Task<IReadOnlyDictionary<Guid, GovernedLifecycleState>> FindKpiDefinitionStatesAsync(
        IReadOnlyCollection<Guid> kpiDefinitionIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = [.. kpiDefinitionIds];
        return await context.Set<KpiDefinition>().AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.LifecycleState, cancellationToken).ConfigureAwait(false);
    }

    public void AddKpiDefinition(KpiDefinition definition) => context.Set<KpiDefinition>().Add(definition);

    public Task<Guid?> FindItemIdAsync(Guid catalogueId, string code, CancellationToken cancellationToken) =>
        context.Set<MasterDataItem>().AsNoTracking().Where(i => i.CatalogueId == catalogueId && i.Code == code).Select(i => (Guid?)i.Id).SingleOrDefaultAsync(cancellationToken);

    public Task<Guid?> FindKpiDefinitionIdAsync(string code, CancellationToken cancellationToken) =>
        context.Set<KpiDefinition>().AsNoTracking().Where(d => d.Code == code).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(cancellationToken);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken) => MasterDataConfigPersistence.SaveAsync(context, cancellationToken);
}
