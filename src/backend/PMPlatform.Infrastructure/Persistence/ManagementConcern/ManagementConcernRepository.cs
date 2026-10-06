using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Domain.ManagementConcern;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Infrastructure.Persistence.ManagementConcern;

/// <summary>The <c>management_concern</c> schema (TASK-057).</summary>
internal sealed class ManagementConcernRepository(PMPlatformDbContext context) : IManagementConcernRepository
{
    public async Task<IConcernWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new ConcernWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new ConcernWork(null);

    public async Task<ConcernEntity?> FindAsync(Guid concernId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ConcernEntity? concern = await context.Set<ConcernEntity>().SingleOrDefaultAsync(c => c.Id == concernId, cancellationToken).ConfigureAwait(false);
        if (concern is not null)
        {
            AdministrationPersistence.ExpectVersion(context, concern, expectedVersion);
        }

        return concern;
    }

    public Task<(IReadOnlyList<ConcernEntity> Items, int TotalCount)> PageAsync(ConcernQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<ConcernEntity> rows = context.Set<ConcernEntity>().AsNoTracking().Where(c => c.ProjectId == query.ProjectId);
        if (query.ConcernTypes.Count > 0)
        {
            rows = rows.Where(c => query.ConcernTypes.Contains(c.ConcernType));
        }

        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(c => query.Statuses.Contains(c.Status));
        }

        if (query.AssigneeUserId is { } assignee)
        {
            rows = rows.Where(c => c.AssigneeUserId == assignee);
        }

        // indexing-strategy.md P-2: updated_at DESC, id DESC, served by I-16.
        return PageAsync(rows.OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id), page, cancellationToken);
    }

    public async Task<IReadOnlyList<ConcernEntity>> ListByOriginatingRisksAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
        await context.Set<ConcernEntity>().AsNoTracking()
            .Where(c => c.OriginatingRiskId != null && riskIds.Contains(c.OriginatingRiskId.Value))
            .OrderBy(c => c.RaisedAt).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ConcernImpact>> ListImpactsAsync(IReadOnlyCollection<Guid> concernIds, CancellationToken cancellationToken) =>
        await context.Set<ConcernImpact>().AsNoTracking().Where(i => concernIds.Contains(i.ManagementConcernId)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ConcernImpact>> FindImpactsAsync(Guid concernId, CancellationToken cancellationToken) =>
        await context.Set<ConcernImpact>().Where(i => i.ManagementConcernId == concernId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ConcernEscalation>> ListOpenEscalationsAsync(IReadOnlyCollection<Guid> concernIds, CancellationToken cancellationToken) =>
        await context.Set<ConcernEscalation>().AsNoTracking()
            .Where(e => concernIds.Contains(e.ManagementConcernId) && e.Status == ConcernEscalationStatus.Open)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ConcernEscalation?> FindEscalationAsync(Guid escalationId, CancellationToken cancellationToken) =>
        context.Set<ConcernEscalation>().SingleOrDefaultAsync(e => e.Id == escalationId, cancellationToken);

    public Task<ConcernEscalation?> FindEscalationByRequestKeyAsync(Guid escalatedByUserId, Guid requestKey, CancellationToken cancellationToken) =>
        context.Set<ConcernEscalation>().AsNoTracking().SingleOrDefaultAsync(e => e.EscalatedByUserId == escalatedByUserId && e.RequestKey == requestKey, cancellationToken);

    public async Task<int> NextEscalationNoAsync(Guid concernId, CancellationToken cancellationToken) =>
        (await context.Set<ConcernEscalation>().Where(e => e.ManagementConcernId == concernId).MaxAsync(e => (int?)e.EscalationNo, cancellationToken).ConfigureAwait(false) ?? 0) + 1;

    public Task<(IReadOnlyList<ConcernEscalation> Items, int TotalCount)> PageEscalationsAsync(Guid concernId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<ConcernEscalation>().AsNoTracking().Where(e => e.ManagementConcernId == concernId).OrderByDescending(e => e.EscalationNo), page, cancellationToken);

    public uint RowVersionOf(ConcernEntity concern) => context.Entry(concern).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public void Add(ConcernEntity concern) => context.Add(concern);

    public void Add(ConcernImpact impact) => context.Add(impact);

    public void Add(ConcernEscalation escalation) => context.Add(escalation);

    public void Remove(ConcernImpact impact) => context.Remove(impact);

    public async Task<ConcernSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ConcernSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return ConcernSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // An escalation's number, the concern's one OPEN escalation, or the escalator's key, taken by another request first.
            context.ChangeTracker.Clear();
            return ConcernSaveOutcome.Duplicate;
        }
    }

    private static async Task<(IReadOnlyList<T> Items, int TotalCount)> PageAsync<T>(IQueryable<T> rows, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<T> items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    /// <summary>Owns its transaction when it began one; joined to the caller's otherwise — the risk's, for an issue raised from it — which the caller commits.</summary>
    private sealed class ConcernWork(IDbContextTransaction? transaction) : IConcernWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
