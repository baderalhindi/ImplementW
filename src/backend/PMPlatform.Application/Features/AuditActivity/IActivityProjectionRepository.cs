using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>The audit store as the Activity projection reads it, and <c>business_activity_entry</c> as it writes it (TASK-073).</summary>
public interface IActivityProjectionRepository
{
    /// <summary>
    /// A unit of work holding the projection's checkpoint locked until it ends, so no other projection pass, check or rebuild runs
    /// meanwhile: what it reads of the projection cannot change under it. Disposing it without committing undoes it.
    /// </summary>
    public Task<IActivityProjectionWork> BeginAsync(CancellationToken cancellationToken);
}

/// <summary>A position in the audit store's commit order.</summary>
public sealed record AuditStorePosition(DateTimeOffset RecordedAt, Guid AuditEventId);

public interface IActivityProjectionWork : IAsyncDisposable
{
    /// <summary>The checkpoint, tracked: a change to it is saved by <see cref="CommitAsync"/>.</summary>
    public ActivityProjectionCheckpoint Checkpoint { get; }

    /// <summary>Up to <paramref name="count"/> events after <paramref name="after"/> (from the first when null), in <c>(recorded_at, id)</c> order, none after <paramref name="upTo"/> when it is set.</summary>
    public Task<IReadOnlyList<AuditEvent>> ReadEventsAsync(AuditStorePosition? after, AuditStorePosition? upTo, int count, CancellationToken cancellationToken);

    /// <summary>How many events follow <paramref name="after"/> (all when null): the projection's lag.</summary>
    public Task<int> CountEventsAfterAsync(AuditStorePosition? after, CancellationToken cancellationToken);

    /// <summary>The stored lines of these events, tracked.</summary>
    public Task<IReadOnlyList<BusinessActivityEntry>> ReadEntriesAsync(IReadOnlyCollection<Guid> auditEventIds, CancellationToken cancellationToken);

    public void Add(BusinessActivityEntry entry);

    public void Remove(BusinessActivityEntry entry);

    /// <summary>Saves the changes and ends the unit of work.</summary>
    public Task CommitAsync(CancellationToken cancellationToken);
}
