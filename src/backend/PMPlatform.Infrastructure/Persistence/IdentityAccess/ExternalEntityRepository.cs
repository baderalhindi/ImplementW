using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>ADM-013 over <c>identity_access.external_entity</c> (TASK-031).</summary>
internal sealed class ExternalEntityRepository(PMPlatformDbContext context) : IExternalEntityRepository
{
    private const string EntityTypeCatalogue = "EXTERNAL_ENTITY_TYPE";

    public async Task<ExternalEntityPage> ListAsync(ExternalEntityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<ExternalEntity> entities = context.Set<ExternalEntity>().AsNoTracking();
        if (query.Statuses.Count > 0)
        {
            entities = entities.Where(e => query.Statuses.Contains(e.Status));
        }

        if (query.EntityTypeItemId is { } entityTypeItemId)
        {
            entities = entities.Where(e => e.EntityTypeItemId == entityTypeItemId);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            string pattern = AdministrationPersistence.ContainsPattern(query.Text.Trim());
            entities = entities.Where(e => EF.Functions.ILike(e.Code, pattern) || EF.Functions.ILike(e.Name.Ar, pattern) || EF.Functions.ILike(e.Name.En, pattern));
        }

        int totalCount = await entities.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ExternalEntitySummary> items = await entities
            .OrderBy(e => e.Code).ThenBy(e => e.Id)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .Select(e => new ExternalEntitySummary(e.Id, e.Code, e.Name, e.EntityTypeItemId, e.Status, e.SponsorUserId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new ExternalEntityPage(items, query.Page.Page, query.Page.PageSize, totalCount);
    }

    public async Task<Versioned<ExternalEntityDetail>?> FindDetailAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var row = await context.Set<ExternalEntity>().AsNoTracking()
            .Where(e => e.Id == entityId)
            .Select(e => new { Entity = e, Version = EF.Property<uint>(e, EntityTypeBuilderExtensions.RowVersion) })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        ExternalEntity entity = row.Entity;
        return new Versioned<ExternalEntityDetail>(
            new ExternalEntityDetail(
                entity.Id, entity.Code, entity.Name, entity.EntityTypeItemId, entity.Status, entity.SponsorUserId,
                entity.CreatedAt, entity.CreatedBy, entity.UpdatedAt, entity.UpdatedBy),
            row.Version);
    }

    public async Task<ExternalEntity?> FindForUpdateAsync(Guid entityId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ExternalEntity? entity = await context.Set<ExternalEntity>().SingleOrDefaultAsync(e => e.Id == entityId, cancellationToken).ConfigureAwait(false);
        if (entity is not null)
        {
            AdministrationPersistence.ExpectVersion(context, entity, expectedVersion);
        }

        return entity;
    }

    public Task<bool> IsEntityTypeAsync(Guid itemId, CancellationToken cancellationToken) =>
        (from item in context.Set<MasterDataItem>().AsNoTracking()
         join catalogue in context.Set<MasterDataCatalogue>() on item.CatalogueId equals catalogue.Id
         where item.Id == itemId && catalogue.Code == EntityTypeCatalogue
         select item.Id)
        .AnyAsync(cancellationToken);

    public void Add(ExternalEntity entity) => context.Set<ExternalEntity>().Add(entity);

    public async Task<IReadOnlyDictionary<Guid, BilingualLabel>> ListNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? []
            : await context.Set<ExternalEntity>().AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken).ConfigureAwait(false);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken) => AdministrationPersistence.SaveAsync(context, cancellationToken);
}
