using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.DataImport;

/// <summary>The import's transaction, which the module repositories join, and the <c>common</c> lineage tables (TASK-102).</summary>
internal sealed class ImportRepository(PMPlatformDbContext context) : IImportRepository
{
    public async Task<IImportWork> BeginAsync(string registerCode, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("An import opens its own transaction.");
        }

        IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // The two-key form keeps the lock apart from the single-key advisory locks the module guards take.
        await context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext('common.import_batch'), hashtext({registerCode}))", cancellationToken).ConfigureAwait(false);
        return new ImportWork(context, transaction);
    }

    public Task<Guid?> FindImportedAsync(string registerCode, string legacyId, CancellationToken cancellationToken) =>
        context.Set<ImportedRecord>().AsNoTracking()
            .Where(r => r.RegisterCode == registerCode && r.LegacyId == legacyId)
            .Select(r => (Guid?)r.EntityId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task SaveBatchAsync(ImportBatch batch, IReadOnlyList<ImportedRecord> records, CancellationToken cancellationToken)
    {
        context.Add(batch);
        context.AddRange(records);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class ImportWork(PMPlatformDbContext context, IDbContextTransaction transaction) : IImportWork
    {
        private const string RowSavepoint = "import_row";

        public Task MarkRowAsync(CancellationToken cancellationToken) => transaction.CreateSavepointAsync(RowSavepoint, cancellationToken);

        public async Task UndoRowAsync(CancellationToken cancellationToken)
        {
            await transaction.RollbackToSavepointAsync(RowSavepoint, cancellationToken).ConfigureAwait(false);
            await transaction.ReleaseSavepointAsync(RowSavepoint, cancellationToken).ConfigureAwait(false);
            context.ChangeTracker.Clear();
        }

        public async Task EndRowAsync(CancellationToken cancellationToken)
        {
            await transaction.ReleaseSavepointAsync(RowSavepoint, cancellationToken).ConfigureAwait(false);
            context.ChangeTracker.Clear();
        }

        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        /// <summary>Uncommitted, the transaction rolls back; nothing it tracked outlives it.</summary>
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            context.ChangeTracker.Clear();
        }
    }
}
