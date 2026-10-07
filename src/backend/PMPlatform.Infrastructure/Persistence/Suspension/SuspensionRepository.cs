using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Suspension;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Suspension;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Suspension;

/// <summary>The <c>suspension</c> schema (TASK-062).</summary>
internal sealed class SuspensionRepository(PMPlatformDbContext context) : ISuspensionRepository
{
    public async Task<ISuspensionWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new SuspensionWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new SuspensionWork(null);

    public async Task<SuspensionRequest?> FindAsync(Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        SuspensionRequest? request = await context.Set<SuspensionRequest>().SingleOrDefaultAsync(r => r.Id == suspensionRequestId, cancellationToken).ConfigureAwait(false);
        if (request is not null)
        {
            AdministrationPersistence.ExpectVersion(context, request, expectedVersion);
        }

        return request;
    }

    public Task<(IReadOnlyList<SuspensionRequest> Items, int TotalCount)> PageAsync(SuspensionRequestQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<SuspensionRequest> rows = context.Set<SuspensionRequest>().AsNoTracking().Where(r => r.ProjectId == query.ProjectId);
        if (query.RequestTypes.Count > 0)
        {
            rows = rows.Where(r => query.RequestTypes.Contains(r.RequestType));
        }

        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(r => query.Statuses.Contains(r.Status));
        }

        // indexing-strategy.md P-2: updated_at DESC, id DESC, served by I-29.
        return PageAsync(rows.OrderByDescending(r => r.UpdatedAt).ThenByDescending(r => r.Id), page, cancellationToken);
    }

    public Task<ActiveSuspension?> FindOpenSuspensionAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Set<ActiveSuspension>().SingleOrDefaultAsync(s => s.ProjectId == projectId && s.EndedAt == null, cancellationToken);

    public async Task<IReadOnlyList<ActiveSuspension>> ListSuspensionsOfAsync(IReadOnlyCollection<Guid> suspensionRequestIds, CancellationToken cancellationToken) =>
        await context.Set<ActiveSuspension>().AsNoTracking()
            .Where(s => suspensionRequestIds.Contains(s.SuspensionRequestId) || (s.ResumptionRequestId != null && suspensionRequestIds.Contains(s.ResumptionRequestId.Value)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<(IReadOnlyList<ActiveSuspension> Items, int TotalCount)> PageSuspensionsAsync(ActiveSuspensionQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<ActiveSuspension> rows = context.Set<ActiveSuspension>().AsNoTracking().Where(s => s.ProjectId == query.ProjectId);
        if (query.Open is { } open)
        {
            rows = open ? rows.Where(s => s.EndedAt == null) : rows.Where(s => s.EndedAt != null);
        }

        return PageAsync(rows.OrderByDescending(s => s.StartedAt).ThenByDescending(s => s.Id), page, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListDueAsync(DateOnly today, int limit, CancellationToken cancellationToken) =>
        await context.Set<SuspensionRequest>().AsNoTracking()
            .Where(r => r.Status == SuspensionRequestStatus.Approved && r.RequestedEffectiveDate <= today)
            .OrderBy(r => r.RequestedEffectiveDate).ThenBy(r => r.Id)
            .Select(r => r.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public uint RowVersionOf(SuspensionRequest request) =>
        context.Entry(request).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public void Add(SuspensionRequest request) => context.Add(request);

    public void Add(ActiveSuspension suspension) => context.Add(suspension);

    public void Remove(SuspensionRequest request) => context.Remove(request);

    public async Task<SuspensionSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return SuspensionSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return SuspensionSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // An open request of the same type, or the project's open suspension, taken by another request first.
            context.ChangeTracker.Clear();
            return SuspensionSaveOutcome.Duplicate;
        }
    }

    private static async Task<(IReadOnlyList<T> Items, int TotalCount)> PageAsync<T>(IQueryable<T> rows, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<T> items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    /// <summary>Owns its transaction when it began one; joined to the caller's otherwise — the outbox dispatch's, for an approval's outcome.</summary>
    private sealed class SuspensionWork(IDbContextTransaction? transaction) : ISuspensionWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
