using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Import;

/// <summary>The project import batches and remembered name mappings (R3-4), in the <c>project</c> schema.</summary>
public interface IProjectImportRepository
{
    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ProjectImportBatch?> FindAsync(Guid batchId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Not tracked, without the file's bytes.</summary>
    public Task<ProjectImportBatch?> ReadAsync(Guid batchId, CancellationToken cancellationToken);

    public uint RowVersionOf(ProjectImportBatch batch);

    /// <summary>Newest first, one page, with the total; without the files' bytes. Not tracked.</summary>
    public Task<(IReadOnlyList<ProjectImportBatch> Items, int TotalCount)> ListAsync(
        ProjectImportBatchStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>The next batch number. Two submissions may draw the same one; the unique key refuses the second, which is retried.</summary>
    public Task<int> NextBatchNoAsync(CancellationToken cancellationToken);

    public void Add(ProjectImportBatch batch);

    /// <summary>Every remembered mapping. Tracked, so a submission can replace one.</summary>
    public Task<IReadOnlyList<ProjectImportNameMapping>> ListMappingsAsync(CancellationToken cancellationToken);

    public void Add(ProjectImportNameMapping mapping);

    public void Add(ProjectImportValidation validation);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it.</summary>
    public Task<ProjectImportValidation?> FindValidationAsync(Guid validationId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The oldest queued validations' ids and versions, for a pass to claim.</summary>
    public Task<IReadOnlyList<(Guid Id, uint Version)>> ListQueuedValidationsAsync(int batchSize, CancellationToken cancellationToken);

    public void Add(ProjectImportValueRequest request);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it.</summary>
    public Task<ProjectImportValueRequest?> FindValueRequestAsync(Guid requestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The OPEN request for this name, if any. Not tracked.</summary>
    public Task<ProjectImportValueRequest?> FindOpenValueRequestAsync(string dimension, string sourceText, CancellationToken cancellationToken);

    /// <summary>Oldest first, one page, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ProjectImportValueRequest> Items, int TotalCount)> ListValueRequestsAsync(
        ProjectImportValueRequestStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    public uint RowVersionOf(ProjectImportValueRequest request);

    /// <summary>Forgets every tracked row, so a worker's next item starts clean.</summary>
    public void Clear();

    /// <summary>
    /// Opens a transaction the project and IdentityAccess repositories join, for the approval to apply a batch whole or not at all. Disposed
    /// uncommitted, it rolls back.
    /// </summary>
    public Task<IAsyncDisposable> BeginAsync(CancellationToken cancellationToken);

    public Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>Saves the tracked changes and whatever else was staged (audit events). False when a row changed since it was read.</summary>
    public Task<bool> SaveAsync(CancellationToken cancellationToken);
}
