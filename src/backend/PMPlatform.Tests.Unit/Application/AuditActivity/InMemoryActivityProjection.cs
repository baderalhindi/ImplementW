using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Tests.Unit.Application.AuditActivity;

/// <summary>The audit store and the projection in memory: a unit of work sees the stored lines and applies its changes only on commit.</summary>
internal sealed class InMemoryActivityProjection : IActivityProjectionRepository
{
    public List<AuditEvent> Events { get; } = [];

    public Dictionary<Guid, BusinessActivityEntry> Entries { get; } = [];

    public ActivityProjectionCheckpoint Checkpoint { get; } = new() { Id = ActivityProjectionCheckpoint.SingletonId };

    public Task<IActivityProjectionWork> BeginAsync(CancellationToken cancellationToken) => Task.FromResult<IActivityProjectionWork>(new Work(this));

    private sealed class Work(InMemoryActivityProjection store) : IActivityProjectionWork
    {
        private readonly List<BusinessActivityEntry> _added = [];
        private readonly List<BusinessActivityEntry> _removed = [];

        public ActivityProjectionCheckpoint Checkpoint { get; } = new()
        {
            Id = store.Checkpoint.Id,
            LastRecordedAt = store.Checkpoint.LastRecordedAt,
            LastAuditEventId = store.Checkpoint.LastAuditEventId,
        };

        public Task<IReadOnlyList<AuditEvent>> ReadEventsAsync(AuditStorePosition? after, AuditStorePosition? upTo, int count, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditEvent>>([.. store.Events
                .Where(e => (after is null || e.RecordedAt > after.RecordedAt) && (upTo is null || e.RecordedAt <= upTo.RecordedAt))
                .OrderBy(e => e.RecordedAt)
                .Take(count)]);

        public Task<int> CountEventsAfterAsync(AuditStorePosition? after, CancellationToken cancellationToken) =>
            Task.FromResult(store.Events.Count(e => after is null || e.RecordedAt > after.RecordedAt));

        public Task<IReadOnlyList<BusinessActivityEntry>> ReadEntriesAsync(IReadOnlyCollection<Guid> auditEventIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BusinessActivityEntry>>([.. store.Entries.Values.Where(e => auditEventIds.Contains(e.AuditEventId))]);

        public void Add(BusinessActivityEntry entry) => _added.Add(entry);

        public void Remove(BusinessActivityEntry entry) => _removed.Add(entry);

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            foreach (BusinessActivityEntry entry in _removed)
            {
                store.Entries.Remove(entry.Id);
            }

            foreach (BusinessActivityEntry entry in _added)
            {
                Assert.DoesNotContain(store.Entries.Values, e => e.AuditEventId == entry.AuditEventId);
                store.Entries.Add(entry.Id, entry);
            }

            store.Checkpoint.LastRecordedAt = Checkpoint.LastRecordedAt;
            store.Checkpoint.LastAuditEventId = Checkpoint.LastAuditEventId;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
