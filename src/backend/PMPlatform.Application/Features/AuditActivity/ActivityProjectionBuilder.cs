using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>Builds the Activity projection from the audit store, and compares and rebuilds it (FG-06 §4.5, AUD-API-035; TASK-073).</summary>
public interface IActivityProjectionBuilder
{
    /// <summary>
    /// Projects up to <paramref name="batchSize"/> stored events after the checkpoint and moves the checkpoint past them, in one
    /// transaction; returns how many events it read. A failure leaves the checkpoint where it was: the store is untouched and the
    /// events are read again on the next pass (FAILED_INDEXING never loses evidence, AUD-CC-19).
    /// </summary>
    public Task<int> ProjectPendingAsync(int batchSize, CancellationToken cancellationToken);

    /// <summary>Compares the projection with what the events up to the checkpoint derive; changes nothing.</summary>
    public Task<ActivityProjectionReport> CheckAsync(CancellationToken cancellationToken);

    /// <summary>Derives the projection again from every stored event, repairs each difference and moves the checkpoint to the head.</summary>
    public Task<ActivityProjectionReport> RebuildAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads the store in commit order, batch by batch, and applies <see cref="ActivityProjection"/>: the same rule for the live pass,
/// the check and the rebuild, so a rebuild reproduces what the live pass built, and a check finds any line that differs.
/// </summary>
internal sealed class ActivityProjectionBuilder(IActivityProjectionRepository repository, TimeProvider timeProvider) : IActivityProjectionBuilder
{
    /// <summary>Events read per round trip by a check or a rebuild.</summary>
    internal const int CompareBatchSize = 500;

    public async Task<int> ProjectPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using IActivityProjectionWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<AuditEvent> events = await work.ReadEventsAsync(PositionOf(work.Checkpoint), upTo: null, batchSize, cancellationToken).ConfigureAwait(false);
        if (events.Count == 0)
        {
            return 0;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        HashSet<Guid> projected = [.. (await work.ReadEntriesAsync([.. events.Select(e => e.Id)], cancellationToken).ConfigureAwait(false)).Select(e => e.AuditEventId)];
        foreach (AuditEvent auditEvent in events.Where(e => !projected.Contains(e.Id)))
        {
            if (ActivityProjection.Project(auditEvent, now) is { } entry)
            {
                work.Add(entry);
            }
        }

        Advance(work.Checkpoint, events[^1], now);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return events.Count;
    }

    public async Task<ActivityProjectionReport> CheckAsync(CancellationToken cancellationToken)
    {
        await using IActivityProjectionWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        AuditStorePosition? checkpoint = PositionOf(work.Checkpoint);
        Comparison comparison = checkpoint is null
            ? new Comparison()
            : await CompareAsync(work, checkpoint, repair: false, cancellationToken).ConfigureAwait(false);
        int pending = await work.CountEventsAfterAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        return comparison.Report(rebuilt: false, pending, work.Checkpoint.LastRecordedAt);
    }

    public async Task<ActivityProjectionReport> RebuildAsync(CancellationToken cancellationToken)
    {
        await using IActivityProjectionWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Comparison comparison = await CompareAsync(work, upTo: null, repair: true, cancellationToken).ConfigureAwait(false);
        if (comparison.Last is { } head)
        {
            Advance(work.Checkpoint, head, timeProvider.GetUtcNow());
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return comparison.Report(rebuilt: true, pendingEvents: 0, work.Checkpoint.LastRecordedAt);
    }

    private async Task<Comparison> CompareAsync(IActivityProjectionWork work, AuditStorePosition? upTo, bool repair, CancellationToken cancellationToken)
    {
        Comparison comparison = new();
        DateTimeOffset now = timeProvider.GetUtcNow();
        AuditStorePosition? after = null;
        while (true)
        {
            IReadOnlyList<AuditEvent> events = await work.ReadEventsAsync(after, upTo, CompareBatchSize, cancellationToken).ConfigureAwait(false);
            if (events.Count == 0)
            {
                return comparison;
            }

            Dictionary<Guid, BusinessActivityEntry> stored = (await work.ReadEntriesAsync([.. events.Select(e => e.Id)], cancellationToken).ConfigureAwait(false))
                .ToDictionary(e => e.AuditEventId);
            foreach (AuditEvent auditEvent in events)
            {
                BusinessActivityEntry? expected = ActivityProjection.Project(auditEvent, now);
                stored.TryGetValue(auditEvent.Id, out BusinessActivityEntry? current);
                ActivityDifferenceKind? difference = (expected, current) switch
                {
                    (null, null) => null,
                    (not null, null) => ActivityDifferenceKind.Missing,
                    (null, not null) => ActivityDifferenceKind.Unexpected,
                    _ => ActivityProjection.SameContent(expected, current) ? null : ActivityDifferenceKind.Changed,
                };

                comparison.Count(auditEvent.Id, expected is not null, difference);
                if (repair && difference is { } kind)
                {
                    Repair(work, kind, expected, current, now);
                }
            }

            comparison.Last = events[^1];
            after = new AuditStorePosition(events[^1].RecordedAt, events[^1].Id);
        }
    }

    private static void Repair(IActivityProjectionWork work, ActivityDifferenceKind kind, BusinessActivityEntry? expected, BusinessActivityEntry? current, DateTimeOffset now)
    {
        switch (kind)
        {
            case ActivityDifferenceKind.Missing:
                work.Add(expected!);
                break;
            case ActivityDifferenceKind.Unexpected:
                work.Remove(current!);
                break;
            case ActivityDifferenceKind.Changed:
                ActivityProjection.CopyContent(expected!, current!, now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown difference.");
        }
    }

    private static AuditStorePosition? PositionOf(ActivityProjectionCheckpoint checkpoint) =>
        checkpoint is { LastRecordedAt: { } recordedAt, LastAuditEventId: { } id } ? new AuditStorePosition(recordedAt, id) : null;

    private static void Advance(ActivityProjectionCheckpoint checkpoint, AuditEvent last, DateTimeOffset now)
    {
        checkpoint.LastRecordedAt = last.RecordedAt;
        checkpoint.LastAuditEventId = last.Id;
        checkpoint.UpdatedAt = now;
        checkpoint.UpdatedBy = AuditCapturePrincipal.Id;
    }

    private sealed class Comparison
    {
        private readonly List<ActivityDifference> _listed = [];
        private int _events;
        private int _expected;
        private int _missing;
        private int _unexpected;
        private int _changed;

        public AuditEvent? Last { get; set; }

        public void Count(Guid auditEventId, bool expected, ActivityDifferenceKind? difference)
        {
            _events++;
            _expected += expected ? 1 : 0;
            switch (difference)
            {
                case null:
                    return;
                case ActivityDifferenceKind.Missing:
                    _missing++;
                    break;
                case ActivityDifferenceKind.Unexpected:
                    _unexpected++;
                    break;
                case ActivityDifferenceKind.Changed:
                    _changed++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(difference), difference, "Unknown difference.");
            }

            if (_listed.Count < ActivityProjectionReport.MaxListedDifferences)
            {
                _listed.Add(new ActivityDifference(auditEventId, difference.Value));
            }
        }

        public ActivityProjectionReport Report(bool rebuilt, int pendingEvents, DateTimeOffset? checkpoint) =>
            new(rebuilt, _events, _expected, _missing, _unexpected, _changed, pendingEvents, checkpoint, _listed);
    }
}
