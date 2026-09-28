namespace PMPlatform.Application.Common.Events;

/// <summary>
/// Where a producer publishes a cross-module message (event-conventions EV-6): into the unit of work its own next save
/// commits, so the fact and its message are committed together or not at all. Dispatch happens after commit.
/// </summary>
public interface IOutbox
{
    public void Stage<TData>(EventEnvelope<TData> envelope)
        where TData : class;
}
