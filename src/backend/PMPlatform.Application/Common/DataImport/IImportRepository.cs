using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Common.DataImport;

/// <summary>The import's transaction and the <c>common.import_batch</c> and <c>common.imported_record</c> lineage.</summary>
public interface IImportRepository
{
    /// <summary>
    /// Opens the one transaction every row of the workbook is written in; the module repositories join it. Two imports of the same
    /// register wait for each other, so neither reads lineage the other has not committed.
    /// </summary>
    public Task<IImportWork> BeginAsync(string registerCode, CancellationToken cancellationToken);

    public Task<Guid?> FindImportedAsync(string registerCode, string legacyId, CancellationToken cancellationToken);

    /// <summary>Saves the batch and its lineage, with whatever else is staged (its audit event), inside the open transaction.</summary>
    public Task SaveBatchAsync(ImportBatch batch, IReadOnlyList<ImportedRecord> records, CancellationToken cancellationToken);
}

/// <summary>
/// The import's transaction. A row is written after <see cref="MarkRowAsync"/> and undone by <see cref="UndoRowAsync"/>, so a rejected row
/// leaves nothing behind while the rows before it stand. Disposed uncommitted, it rolls everything back.
/// </summary>
public interface IImportWork : IAsyncDisposable
{
    public Task MarkRowAsync(CancellationToken cancellationToken);

    /// <summary>Back to the last mark, forgetting whatever the row left unsaved.</summary>
    public Task UndoRowAsync(CancellationToken cancellationToken);

    /// <summary>Keeps the saved row and forgets its tracked state, so a workbook of thousands of rows does not track them all.</summary>
    public Task EndRowAsync(CancellationToken cancellationToken);

    public Task CommitAsync(CancellationToken cancellationToken);
}
