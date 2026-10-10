using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>The <c>reports</c> schema (TASK-071). Finds that return rows to change track them; the others do not.</summary>
public interface IReportRepository
{
    /// <summary>Each report's PUBLISHED version. Not tracked.</summary>
    public Task<IReadOnlyList<ReportDefinition>> ListPublishedAsync(CancellationToken cancellationToken);

    /// <summary>The report's PUBLISHED version; tracked when <paramref name="track"/>.</summary>
    public Task<ReportDefinition?> FindPublishedAsync(ReportCode code, bool track, CancellationToken cancellationToken);

    /// <summary>One page of versions, the newest of each report first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ReportDefinition> Items, int TotalCount)> PageDefinitionsAsync(ReportDefinitionQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ReportDefinition?> FindDefinitionAsync(Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>A version by id, in any state. Not tracked: the version a job or a saved view names.</summary>
    public Task<ReportDefinition?> ReadDefinitionAsync(Guid definitionId, CancellationToken cancellationToken);

    /// <summary>The report's version on its way, DRAFT or VALIDATED, if any. Not tracked.</summary>
    public Task<ReportDefinition?> FindOpenAsync(ReportCode code, CancellationToken cancellationToken);

    /// <summary>The report's highest version number; 0 before its first.</summary>
    public Task<int> MaxVersionNoAsync(ReportCode code, CancellationToken cancellationToken);

    /// <summary>A version's columns in their order; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<ReportColumn>> ListColumnsAsync(Guid definitionId, bool track, CancellationToken cancellationToken);

    /// <summary>The audience of the versions named; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<ReportAudienceRole>> ListAudienceAsync(IReadOnlyCollection<Guid> definitionIds, bool track, CancellationToken cancellationToken);

    /// <summary>A version's parameters in their order; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<ReportParameter>> ListParametersAsync(Guid definitionId, bool track, CancellationToken cancellationToken);

    /// <summary>The options of the parameters named; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<ReportParameterOption>> ListOptionsAsync(IReadOnlyCollection<Guid> parameterIds, bool track, CancellationToken cancellationToken);

    /// <summary>One page of a person's saved views, the most recently changed first. Not tracked.</summary>
    public Task<(IReadOnlyList<SavedView> Items, int TotalCount)> PageViewsAsync(Guid ownerUserId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<SavedView?> FindViewAsync(Guid savedViewId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<IReadOnlyList<SavedViewColumn>> ListViewColumnsAsync(Guid savedViewId, bool track, CancellationToken cancellationToken);

    public Task<IReadOnlyList<SavedViewFilter>> ListViewFiltersAsync(Guid savedViewId, bool track, CancellationToken cancellationToken);

    public Task<IReadOnlyList<ReportParameterValue>> ListViewParametersAsync(Guid savedViewId, bool track, CancellationToken cancellationToken);

    /// <summary>The requester's job for their key, if they made one. Not tracked.</summary>
    public Task<ReportJob?> FindJobByKeyAsync(Guid requestedByUserId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>One page of a requester's jobs, the newest first. Not tracked.</summary>
    public Task<(IReadOnlyList<ReportJob> Items, int TotalCount)> PageJobsAsync(Guid requestedByUserId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ReportJob?> FindJobAsync(Guid jobId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The oldest jobs in <paramref name="status"/>, up to <paramref name="batchSize"/>.</summary>
    public Task<IReadOnlyList<Guid>> ListJobIdsAsync(ReportJobStatus status, int batchSize, CancellationToken cancellationToken);

    /// <summary>Jobs left VALIDATING or RUNNING since before <paramref name="before"/>: their worker stopped mid-way.</summary>
    public Task<IReadOnlyList<Guid>> ListAbandonedJobIdsAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken);

    /// <summary>The jobs of outputs still on file whose expiry has come.</summary>
    public Task<IReadOnlyList<Guid>> ListExpiredJobIdsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    /// <summary>The output of each job named that has one, by job. Not tracked.</summary>
    public Task<IReadOnlyDictionary<Guid, GeneratedOutput>> ListOutputsAsync(IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken);

    /// <summary>A job's output. Tracked.</summary>
    public Task<GeneratedOutput?> FindOutputAsync(Guid jobId, CancellationToken cancellationToken);

    /// <summary>The stored bytes under an output's key. Not tracked.</summary>
    public Task<byte[]?> ReadContentAsync(string storageObjectKey, CancellationToken cancellationToken);

    /// <summary>Removes the stored bytes under an output's key, with the next save.</summary>
    public Task RemoveContentAsync(string storageObjectKey, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked row, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(AuditedEntity row);

    public void Add(AuditedEntity row);

    public void Remove(AuditedEntity row);

    /// <summary>
    /// Saves the tracked changes and the audit events this unit of work staged. A row changed since it was read, and a key another request took
    /// first — a version number, the version on its way, a job's idempotency key — are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<ReportSaveOutcome> SaveAsync(CancellationToken cancellationToken);

    /// <summary>Forgets every tracked change: after a failed generation, so the job's failure is saved alone.</summary>
    public void Clear();
}

public enum ReportSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21): another worker claimed the job first, or the requester cancelled it.</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key, or the at-commit one-PUBLISHED-version check, refused the save.</summary>
    Duplicate = 3,
}
