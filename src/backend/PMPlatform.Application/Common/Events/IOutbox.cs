namespace PMPlatform.Application.Common.Events;

/// <summary>
/// Where a producer publishes a cross-module message (event-conventions EV-6): into the unit of work its own next save
/// commits, so the fact and its message are committed together or not at all. Dispatch happens after commit.
/// </summary>
public interface IOutbox
{
    public void Stage<TData>(EventEnvelope<TData> envelope)
        where TData : class;

    /// <summary>
    /// Commits the message now, in a transaction of its own, for an occurrence that changes nothing of the producer's — a call
    /// to an external provider (TASK-075). Nothing pending in the caller's unit of work is committed with it. Throws when the
    /// message cannot be committed; the caller decides whether that may fail its own work.
    /// </summary>
    public Task RecordAsync<TData>(EventEnvelope<TData> envelope, CancellationToken cancellationToken)
        where TData : class;
}
