using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>
/// Rows held in memory with a version each, as <c>xmin</c> would give them: a save fails as a concurrency conflict if a row
/// was read for update at a version it no longer has, and moves every saved row on by one.
/// </summary>
internal abstract class FakeStore<TEntity>
    where TEntity : AuditedEntity
{
    private readonly List<(Guid Id, uint Expected)> _expectations = [];

    public Dictionary<Guid, TEntity> Rows { get; } = [];

    public Dictionary<Guid, uint> Versions { get; } = [];

    /// <summary>What the next save answers instead, e.g. a duplicate key the database would report.</summary>
    public SaveResult? NextSaveResult { get; set; }

    public int Saves { get; private set; }

    public void Put(TEntity entity)
    {
        Rows[entity.Id] = entity;
        Versions.TryAdd(entity.Id, 1);
    }

    public void Add(TEntity entity) => Put(entity);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Saves++;
        SaveResult result = NextSaveResult
                            ?? (_expectations.Any(e => Versions.GetValueOrDefault(e.Id) != e.Expected) ? SaveResult.ConcurrencyConflict : SaveResult.Saved);
        _expectations.Clear();
        NextSaveResult = null;
        if (result.Outcome == SaveOutcome.Saved)
        {
            foreach (Guid id in Rows.Keys)
            {
                Versions[id] = Versions.GetValueOrDefault(id) + 1;
            }
        }

        return Task.FromResult(result);
    }

    protected TEntity? Track(Guid id, uint? expectedVersion)
    {
        if (expectedVersion is { } version)
        {
            _expectations.Add((id, version));
        }

        return Rows.GetValueOrDefault(id);
    }

    protected static TPage PageOf<TItem, TPage>(IEnumerable<TItem> items, PageRequest page, Func<IReadOnlyList<TItem>, int, TPage> create)
    {
        List<TItem> all = [.. items];
        return create([.. all.Skip(page.Skip).Take(page.PageSize)], all.Count);
    }
}
