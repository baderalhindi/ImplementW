using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>
/// The Activity projection's unit of work (TASK-073): a transaction that creates the checkpoint row if it is missing and holds it
/// <c>FOR UPDATE</c> until the work ends, so projection passes, checks and rebuilds run one at a time across every API instance. A
/// second pass waits for the first and then reads past it, so an event committed during the first is projected by the second.
/// </summary>
internal sealed class ActivityProjectionRepository(PMPlatformDbContext context) : IActivityProjectionRepository
{
    public async Task<IActivityProjectionWork> BeginAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Guid principal = AuditCapturePrincipal.Id;
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO audit_activity.activity_projection_checkpoint (id, created_at, created_by, updated_at, updated_by)
                VALUES ({ActivityProjectionCheckpoint.SingletonId}, now(), {principal}, now(), {principal})
                ON CONFLICT (id) DO NOTHING
                """, cancellationToken).ConfigureAwait(false);
            ActivityProjectionCheckpoint checkpoint = (await context.Set<ActivityProjectionCheckpoint>()
                    .FromSql($"SELECT * FROM audit_activity.activity_projection_checkpoint WHERE id = {ActivityProjectionCheckpoint.SingletonId} FOR UPDATE")
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .Single();
            return new Work(context, transaction, checkpoint);
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class Work(PMPlatformDbContext context, IDbContextTransaction transaction, ActivityProjectionCheckpoint checkpoint) : IActivityProjectionWork
    {
        public ActivityProjectionCheckpoint Checkpoint { get; } = checkpoint;

        public async Task<IReadOnlyList<AuditEvent>> ReadEventsAsync(AuditStorePosition? after, AuditStorePosition? upTo, int count, CancellationToken cancellationToken) =>
            await After(after).Where(UpTo(upTo))
                .OrderBy(e => e.RecordedAt).ThenBy(e => e.Id)
                .Take(count)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public Task<int> CountEventsAfterAsync(AuditStorePosition? after, CancellationToken cancellationToken) => After(after).CountAsync(cancellationToken);

        public async Task<IReadOnlyList<BusinessActivityEntry>> ReadEntriesAsync(IReadOnlyCollection<Guid> auditEventIds, CancellationToken cancellationToken) =>
            await context.Set<BusinessActivityEntry>().Where(e => auditEventIds.Contains(e.AuditEventId)).ToListAsync(cancellationToken).ConfigureAwait(false);

        public void Add(BusinessActivityEntry entry) => context.Add(entry);

        public void Remove(BusinessActivityEntry entry) => context.Remove(entry);

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();

        /// <summary>
        /// Events after the position, read untracked, as evidence is never changed. The chain trigger gives every event a
        /// <c>recorded_at</c> later than its predecessor's (TASK-033 D-3), so <c>recorded_at</c> alone orders the store.
        /// </summary>
        private IQueryable<AuditEvent> After(AuditStorePosition? after)
        {
            IQueryable<AuditEvent> events = context.Set<AuditEvent>().AsNoTracking();
            if (after is null)
            {
                return events;
            }

            DateTimeOffset recordedAt = after.RecordedAt;
            return events.Where(e => e.RecordedAt > recordedAt);
        }

        private static System.Linq.Expressions.Expression<Func<AuditEvent, bool>> UpTo(AuditStorePosition? upTo)
        {
            if (upTo is null)
            {
                return _ => true;
            }

            DateTimeOffset recordedAt = upTo.RecordedAt;
            return e => e.RecordedAt <= recordedAt;
        }
    }
}
