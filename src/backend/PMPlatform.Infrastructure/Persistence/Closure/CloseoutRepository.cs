using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.Closure;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Closure;

/// <summary>The <c>closure</c> schema (TASK-063).</summary>
internal sealed class CloseoutRepository(PMPlatformDbContext context) : ICloseoutRepository
{
    public async Task<ICloseoutWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new CloseoutWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new CloseoutWork(null);

    public async Task<TCase?> FindCaseAsync<TCase>(Guid caseId, uint? expectedVersion, CancellationToken cancellationToken)
        where TCase : CloseoutCase
    {
        TCase? @case = await context.Set<TCase>().SingleOrDefaultAsync(c => c.Id == caseId, cancellationToken).ConfigureAwait(false);
        if (@case is not null)
        {
            AdministrationPersistence.ExpectVersion(context, @case, expectedVersion);
        }

        return @case;
    }

    public Task<(IReadOnlyList<TCase> Items, int TotalCount)> PageCasesAsync<TCase>(CloseoutCaseQuery query, PageRequest page, CancellationToken cancellationToken)
        where TCase : CloseoutCase
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<TCase> rows = context.Set<TCase>().AsNoTracking().Where(c => c.ProjectId == query.ProjectId);
        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(c => query.Statuses.Contains(c.Status));
        }

        // indexing-strategy.md P-2: updated_at DESC, id DESC.
        return PageAsync(rows.OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id), page, cancellationToken);
    }

    public Task<CompletionCase?> FindEffectedCompletionAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Set<CompletionCase>().AsNoTracking().SingleOrDefaultAsync(c => c.ProjectId == projectId && c.Status == CloseoutCaseStatus.Effected, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListApprovedAsync<TCase>(int limit, CancellationToken cancellationToken)
        where TCase : CloseoutCase =>
        await context.Set<TCase>().AsNoTracking()
            .Where(c => c.Status == CloseoutCaseStatus.Approved)
            .OrderBy(c => c.UpdatedAt).ThenBy(c => c.Id)
            .Select(c => c.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ReadinessCheck>> ListReadinessAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken) =>
        await context.Set<ReadinessCheck>().AsNoTracking()
            .Where(r => (r.CompletionCaseId != null && caseIds.Contains(r.CompletionCaseId.Value)) || (r.ClosureCaseId != null && caseIds.Contains(r.ClosureCaseId.Value)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<(IReadOnlyList<ReadinessCheck> Items, int TotalCount)> PageReadinessAsync(ReadinessRecordQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<ReadinessCheck> rows = context.Set<ReadinessCheck>().AsNoTracking();
        rows = query.CompletionCaseId is { } completionId ? rows.Where(r => r.CompletionCaseId == completionId) : rows.Where(r => r.ClosureCaseId == query.ClosureCaseId);
        return PageAsync(rows.OrderByDescending(r => r.EvaluatedAt).ThenBy(r => r.CheckCode).ThenByDescending(r => r.Id), page, cancellationToken);
    }

    public async Task<IReadOnlyList<PostProjectObligation>> ListObligationsAsync(Guid projectId, CancellationToken cancellationToken) =>
        await context.Set<PostProjectObligation>().AsNoTracking().Where(o => o.ProjectId == projectId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<PostProjectObligation?> FindObligationAsync(Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        PostProjectObligation? obligation = await context.Set<PostProjectObligation>().SingleOrDefaultAsync(o => o.Id == obligationId, cancellationToken).ConfigureAwait(false);
        if (obligation is not null)
        {
            AdministrationPersistence.ExpectVersion(context, obligation, expectedVersion);
        }

        return obligation;
    }

    public Task<(IReadOnlyList<PostProjectObligation> Items, int TotalCount)> PageObligationsAsync(
        PostProjectObligationQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<PostProjectObligation> rows = context.Set<PostProjectObligation>().AsNoTracking().Where(o => o.ProjectId == query.ProjectId);
        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(o => query.Statuses.Contains(o.Status));
        }

        return PageAsync(rows.OrderByDescending(o => o.UpdatedAt).ThenByDescending(o => o.Id), page, cancellationToken);
    }

    public uint RowVersionOf(AuditedEntity row) => context.Entry(row).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public void Add(AuditedEntity row) => context.Add(row);

    public void Remove(AuditedEntity row) => context.Remove(row);

    public void Discard() => context.ChangeTracker.Clear();

    public async Task<CloseoutSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return CloseoutSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return CloseoutSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // An open case of the same kind, taken by another request first.
            context.ChangeTracker.Clear();
            return CloseoutSaveOutcome.Duplicate;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // A deleted draft that readiness records or obligations still name.
            context.ChangeTracker.Clear();
            return CloseoutSaveOutcome.InUse;
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
    private sealed class CloseoutWork(IDbContextTransaction? transaction) : ICloseoutWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
