using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.Messaging;

/// <summary>
/// Delivers DOMAIN_EVENT messages to their one consumer (event-conventions EV-6), each in its own scope and transaction.
/// The message row is locked (<c>FOR UPDATE SKIP LOCKED</c>) and re-read as undispatched first, then the consumer runs
/// and the message is marked dispatched in that same transaction. Whatever the consumer saves through the scope's
/// context therefore commits exactly when the mark does: a retry, a second worker or a second API instance finds the
/// message dispatched, or locked, and does nothing. A consumer failure rolls both back; the attempt is counted and the
/// message waits its back-off. After <see cref="MaxAttempts"/> it is left undispatched with its last error (dead letter).
/// </summary>
internal sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopes, IOptions<OutboxOptions> options, TimeProvider timeProvider, ILogger<OutboxDispatcher> logger)
    : IOutboxDispatcher
{
    public const int MaxAttempts = 5;

    /// <summary>The SERVICE principal (db/seed <c>svc.outbox-dispatch</c>) recorded as the author of a dispatch or a failed attempt.</summary>
    public static readonly Guid DispatchPrincipalId = new("00000000-0000-4000-8000-0000000000fb");

    public async Task<int> DispatchDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        List<Guid> due;
        await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            due = await scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>().Set<OutboxMessage>().AsNoTracking()
                .Where(m => m.DispatchedAt == null && m.MessageType == EventKind.DomainEvent && m.AttemptCount < MaxAttempts
                            && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
                .OrderBy(m => m.OccurredAt).ThenBy(m => m.Id)
                .Select(m => m.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        int delivered = 0;
        foreach (Guid messageId in due)
        {
            delivered += await DispatchAsync(messageId, cancellationToken).ConfigureAwait(false) ? 1 : 0;
        }

        return delivered;
    }

    public async Task<bool> DispatchAsync(Guid messageId, CancellationToken cancellationToken)
    {
        string failure;
        await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
        {
            PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            OutboxMessage? message = (await context.Set<OutboxMessage>()
                    .FromSql($"""
                        SELECT * FROM common.outbox_message
                        WHERE id = {messageId} AND dispatched_at IS NULL AND message_type = 'DOMAIN_EVENT'
                        FOR UPDATE SKIP LOCKED
                        """)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .SingleOrDefault();
            if (message is null)
            {
                return false;
            }

            try
            {
                string eventType = EventMessageKey.EventTypeOf(message.MessageKey);
                IDomainEventConsumer consumer = scope.ServiceProvider.GetServices<IDomainEventConsumer>().Where(c => c.EventType == eventType).ToList() switch
                {
                    [var one] => one,
                    [] => throw new InvalidOperationException($"No consumer is registered for {eventType}."),
                    _ => throw new InvalidOperationException($"More than one consumer is registered for {eventType}."),
                };
                await consumer.HandleAsync(message.Payload, cancellationToken).ConfigureAwait(false);

                DateTimeOffset now = timeProvider.GetUtcNow();
                message.DispatchedAt = now;
                message.AttemptCount++;
                message.NextAttemptAt = null;
                message.LastError = null;
                message.UpdatedAt = now;
                message.UpdatedBy = DispatchPrincipalId;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                failure = exception.GetType().Name;
                LogDeliveryFailed(logger, message.MessageKey, failure);
            }
        }

        await RecordFailureAsync(messageId, failure, cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <summary>In a transaction of its own, after the failed one was rolled back.</summary>
    private async Task RecordFailureAsync(Guid messageId, string failure, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
        OutboxMessage message = await context.Set<OutboxMessage>().SingleAsync(m => m.Id == messageId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = timeProvider.GetUtcNow();
        message.AttemptCount++;
        message.NextAttemptAt = message.AttemptCount < MaxAttempts ? now + (options.Value.RetryBaseDelay * Math.Pow(2, message.AttemptCount - 1)) : null;
        message.LastError = failure;
        message.UpdatedAt = now;
        message.UpdatedBy = DispatchPrincipalId;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageKey} was not delivered: {Reason}. It is tried again after its back-off.")]
    private static partial void LogDeliveryFailed(ILogger logger, string messageKey, string reason);
}
