using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Domain.ManagementConcern;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// The <c>management_concern</c> schema (TASK-057). Finds that return rows to change track them; the others do not. A write to a
/// concern's impacts also touches the concern row, so the concern's row version serialises them (R-21); two escalations of one
/// concern meet on its unique keys.
/// </summary>
public interface IManagementConcernRepository
{
    public Task<IConcernWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ConcernEntity?> FindAsync(Guid concernId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>One page of the project's concerns the query selects, most recently changed first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ConcernEntity> Items, int TotalCount)> PageAsync(ConcernQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The issues raised from any of the risks, oldest first. Not tracked.</summary>
    public Task<IReadOnlyList<ConcernEntity>> ListByOriginatingRisksAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken);

    /// <summary>The impacts of the concerns. Not tracked.</summary>
    public Task<IReadOnlyList<ConcernImpact>> ListImpactsAsync(IReadOnlyCollection<Guid> concernIds, CancellationToken cancellationToken);

    /// <summary>The concern's impacts. Tracked, to be replaced.</summary>
    public Task<IReadOnlyList<ConcernImpact>> FindImpactsAsync(Guid concernId, CancellationToken cancellationToken);

    /// <summary>The OPEN escalation of each of the concerns that has one. Not tracked.</summary>
    public Task<IReadOnlyList<ConcernEscalation>> ListOpenEscalationsAsync(IReadOnlyCollection<Guid> concernIds, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<ConcernEscalation?> FindEscalationAsync(Guid escalationId, CancellationToken cancellationToken);

    /// <summary>The escalation the escalator raised with this <c>Idempotency-Key</c>, if any (R-37: a key is the principal's). Not tracked.</summary>
    public Task<ConcernEscalation?> FindEscalationByRequestKeyAsync(Guid escalatedByUserId, Guid requestKey, CancellationToken cancellationToken);

    /// <summary>The number the concern's next escalation takes: one after its last.</summary>
    public Task<int> NextEscalationNoAsync(Guid concernId, CancellationToken cancellationToken);

    /// <summary>One page of the concern's escalations, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ConcernEscalation> Items, int TotalCount)> PageEscalationsAsync(Guid concernId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>How many of the project's concerns are in each status, for WF-10's readiness (TASK-063). Not tracked.</summary>
    public Task<IReadOnlyDictionary<ConcernStatus, int>> CountByStatusAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked concern, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(ConcernEntity concern);

    public void Add(ConcernEntity concern);

    public void Add(ConcernImpact impact);

    public void Add(ConcernEscalation escalation);

    public void Remove(ConcernImpact impact);

    /// <summary>
    /// Saves the tracked changes and what this unit of work staged — audit events, outbox messages, WF-11 runs. A row changed since it
    /// was read, and a unique key another request took first, are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<ConcernSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A unit of work over the schema; disposing it without <see cref="CommitAsync"/> rolls it back. Joins a transaction already open.</summary>
public interface IConcernWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum ConcernSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key another request took first: an escalation's number, the concern's one OPEN escalation, or the escalator's key.</summary>
    Duplicate = 3,
}
