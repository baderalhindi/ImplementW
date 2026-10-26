using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// The <c>closure</c> schema (TASK-063). Finds that return rows to change track them; the others do not. A case's row version serialises
/// its commands (R-21). Partial unique keys hold the single-instance rules whoever writes: one open case of each kind per project, and one
/// effected case of each kind per project.
/// </summary>
public interface ICloseoutRepository
{
    public Task<ICloseoutWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<TCase?> FindCaseAsync<TCase>(Guid caseId, uint? expectedVersion, CancellationToken cancellationToken)
        where TCase : CloseoutCase;

    /// <summary>One page of the project's cases of one kind the query selects, most recently changed first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<TCase> Items, int TotalCount)> PageCasesAsync<TCase>(CloseoutCaseQuery query, PageRequest page, CancellationToken cancellationToken)
        where TCase : CloseoutCase;

    /// <summary>The project's EFFECTED completion case; null when it has none. Not tracked.</summary>
    public Task<CompletionCase?> FindEffectedCompletionAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The ids of up to <paramref name="limit"/> APPROVED cases of one kind, oldest decision first.</summary>
    public Task<IReadOnlyList<Guid>> ListApprovedAsync<TCase>(int limit, CancellationToken cancellationToken)
        where TCase : CloseoutCase;

    /// <summary>Every readiness record of the given cases, of either kind. Not tracked.</summary>
    public Task<IReadOnlyList<ReadinessCheck>> ListReadinessAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken);

    /// <summary>One page of a case's readiness records, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ReadinessCheck> Items, int TotalCount)> PageReadinessAsync(ReadinessRecordQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Every obligation of the project. Not tracked.</summary>
    public Task<IReadOnlyList<PostProjectObligation>> ListObligationsAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<PostProjectObligation?> FindObligationAsync(Guid obligationId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>One page of the project's obligations the query selects, most recently changed first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<PostProjectObligation> Items, int TotalCount)> PageObligationsAsync(
        PostProjectObligationQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked row, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(AuditedEntity row);

    public void Add(AuditedEntity row);

    public void Remove(AuditedEntity row);

    /// <summary>Forgets every tracked change, so a refused activation in a pass leaves nothing for the next case's save.</summary>
    public void Discard();

    /// <summary>
    /// Saves the tracked changes and what this unit of work staged — audit events, a WF-11 run, the project's lifecycle change, the end
    /// of its suspension. A row changed since it was read, a unique key another case took first, and a deleted draft something references
    /// are answers, not faults; after any of them nothing stays tracked.
    /// </summary>
    public Task<CloseoutSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A unit of work over the schema; disposing it without <see cref="CommitAsync"/> rolls it back. Joins a transaction already open.</summary>
public interface ICloseoutWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum CloseoutSaveOutcome
{
    Saved = 1,

    /// <summary>A row changed since it was read (R-21): the case, the obligation, or the project another command moved meanwhile.</summary>
    ConcurrencyConflict = 2,

    /// <summary>A single-instance key another case took first: an open case of the same kind for the project.</summary>
    Duplicate = 3,

    /// <summary>A deleted draft is referenced: it has readiness records or obligations, which are kept.</summary>
    InUse = 4,
}
