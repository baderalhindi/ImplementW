using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// The <c>suspension</c> schema (TASK-062). Finds that return rows to change track them; the others do not. A request's row version
/// serialises its commands (R-21). Two unique keys hold the module's single-instance rules whoever writes: one open request of each type
/// per project, and one open suspension per project.
/// </summary>
public interface ISuspensionRepository
{
    public Task<ISuspensionWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<SuspensionRequest?> FindAsync(Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>One page of the project's requests the query selects, most recently changed first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<SuspensionRequest> Items, int TotalCount)> PageAsync(SuspensionRequestQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The project's open suspension, tracked; null when it has none.</summary>
    public Task<ActiveSuspension?> FindOpenSuspensionAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The suspension periods each request opened or ended. Not tracked.</summary>
    public Task<IReadOnlyList<ActiveSuspension>> ListSuspensionsOfAsync(IReadOnlyCollection<Guid> suspensionRequestIds, CancellationToken cancellationToken);

    /// <summary>One page of the project's suspension periods, most recently started first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ActiveSuspension> Items, int TotalCount)> PageSuspensionsAsync(ActiveSuspensionQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The ids of up to <paramref name="limit"/> APPROVED requests whose effective date is on or before <paramref name="today"/>, earliest first.</summary>
    public Task<IReadOnlyList<Guid>> ListDueAsync(DateOnly today, int limit, CancellationToken cancellationToken);

    /// <summary>How many of the project's suspension and resumption requests are in each status, for WF-10's readiness (TASK-063). Not tracked.</summary>
    public Task<IReadOnlyDictionary<SuspensionRequestStatus, int>> CountByStatusAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked request, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(SuspensionRequest request);

    public void Add(SuspensionRequest request);

    public void Add(ActiveSuspension suspension);

    public void Remove(SuspensionRequest request);

    /// <summary>
    /// Saves the tracked changes and what this unit of work staged — audit events, a WF-11 run, the project's lifecycle change. A row
    /// changed since it was read, and a unique key another request took first, are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<SuspensionSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A unit of work over the schema; disposing it without <see cref="CommitAsync"/> rolls it back. Joins a transaction already open.</summary>
public interface ISuspensionWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum SuspensionSaveOutcome
{
    Saved = 1,

    /// <summary>A row changed since it was read (R-21): the request, or the project another command moved meanwhile.</summary>
    ConcurrencyConflict = 2,

    /// <summary>A single-instance key another request took first: an open request of the same type, or the project's open suspension.</summary>
    Duplicate = 3,
}
