using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

internal sealed class FakeExternalEntityRepository : FakeStore<ExternalEntity>, IExternalEntityRepository
{
    public HashSet<Guid> EntityTypes { get; } = [];

    public Task<ExternalEntityPage> ListAsync(ExternalEntityQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(PageOf(
            Rows.Values.OrderBy(e => e.Code, StringComparer.Ordinal).Select(e => new ExternalEntitySummary(e.Id, e.Code, e.Name, e.EntityTypeItemId, e.Status, e.SponsorUserId)),
            query.Page,
            (items, total) => new ExternalEntityPage(items, query.Page.Page, query.Page.PageSize, total)));

    public Task<Versioned<ExternalEntityDetail>?> FindDetailAsync(Guid entityId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.TryGetValue(entityId, out ExternalEntity? e)
            ? new Versioned<ExternalEntityDetail>(
                new ExternalEntityDetail(e.Id, e.Code, e.Name, e.EntityTypeItemId, e.Status, e.SponsorUserId, e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy),
                Versions[e.Id])
            : null);

    public Task<ExternalEntity?> FindForUpdateAsync(Guid entityId, uint? expectedVersion, CancellationToken cancellationToken) =>
        Task.FromResult(Track(entityId, expectedVersion));

    public Task<bool> IsEntityTypeAsync(Guid itemId, CancellationToken cancellationToken) => Task.FromResult(EntityTypes.Contains(itemId));

    public Task<IReadOnlyDictionary<Guid, BilingualLabel>> ListNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, BilingualLabel>>(Rows.Values.Where(r => ids.Contains(r.Id)).ToDictionary(r => r.Id, r => r.Name));
}
