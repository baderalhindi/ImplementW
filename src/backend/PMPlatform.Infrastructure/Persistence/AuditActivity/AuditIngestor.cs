using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>
/// Stores committed <c>AUDIT_EVENT</c> messages (FG-06 §4.2; TASK-073). Each message is handled in a transaction of its own: its row
/// is locked (<c>FOR UPDATE SKIP LOCKED</c>) and re-read as undispatched, the capture is read and validated (RECEIVED → VALIDATED),
/// the event is inserted unless an event with its id is already stored (AUD-CC-07), and the message is marked dispatched (STORED),
/// all committed together. The hash chain's lock is held only for that short transaction, not for the producer's. A message that
/// cannot be read, validated or stored is rolled back, its failure is recorded with an exponential back-off capped at
/// <see cref="AuditIngestionOptions.MaxRetryDelay"/>, and it is tried again for as long as it takes (FAILED_INGESTION, AUD-CC-28).
/// </summary>
internal sealed partial class AuditIngestor(
    IServiceScopeFactory scopes, AuditRecordFactory records, IOptions<AuditIngestionOptions> options, TimeProvider timeProvider, ILogger<AuditIngestor> logger)
    : IAuditIngestor
{
    /// <summary>The SERVICE principal (db/seed <c>svc.audit-capture</c>) recorded as the author of a dispatch or a failed attempt.</summary>
    private static readonly Guid IngestionPrincipalId = AuditCapturePrincipal.Id;

    public async Task<int> IngestAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        int stored = 0;
        foreach (Guid messageId in messageIds)
        {
            stored += await IngestOneAsync(messageId, cancellationToken).ConfigureAwait(false) ? 1 : 0;
        }

        return stored;
    }

    public async Task<int> IngestDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        List<Guid> due;
        await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            due = await scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>().Set<OutboxMessage>().AsNoTracking()
                .Where(m => m.MessageType == EventKind.AuditEvent && m.DispatchedAt == null && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
                .OrderBy(m => m.OccurredAt).ThenBy(m => m.Id)
                .Select(m => m.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return await IngestAsync(due, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IngestOneAsync(Guid messageId, CancellationToken cancellationToken)
    {
        string failure;
        await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
        {
            PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // Ingestion at commit runs inside the producer's request: it must never wait on a lock that request still holds (a row
            // its open transaction locked, which an event's foreign key checks). It gives up instead, and the worker stores the event.
            await context.Database.ExecuteSqlRawAsync(LockTimeoutStatement(options.Value.LockTimeout), cancellationToken).ConfigureAwait(false);
            OutboxMessage? message = (await context.Set<OutboxMessage>()
                    .FromSql($"""
                        SELECT * FROM common.outbox_message
                        WHERE id = {messageId} AND message_type = 'AUDIT_EVENT' AND dispatched_at IS NULL
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
                AuditCapture capture = Validated(message.Payload);
                bool alreadyStored = await context.Set<AuditEvent>().AnyAsync(e => e.Id == capture.EventId, cancellationToken).ConfigureAwait(false);
                if (!alreadyStored)
                {
                    AuditRecord record = records.Create(capture);
                    context.Add(record.Event);
                    context.AddRange(record.Attributes);
                    if (record.Forwarding is { } forwarding)
                    {
                        context.Add(forwarding);
                    }
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                message.DispatchedAt = now;
                message.AttemptCount++;
                message.NextAttemptAt = null;
                message.LastError = null;
                message.UpdatedAt = now;
                message.UpdatedBy = IngestionPrincipalId;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return !alreadyStored;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                failure = exception is AuditCaptureRejectedException rejected ? rejected.Code : exception.GetType().Name;
                LogIngestionFailed(logger, messageId, failure);
            }
        }

        await RecordFailureAsync(messageId, failure, cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <summary>The capture, redacted again and checked as the trail checks it: a message that did not come through the trail is never trusted (AUD-CC-14).</summary>
    private static AuditCapture Validated(string payload)
    {
        AuditCapture capture;
        try
        {
            capture = AuditCapture.Deserialize(payload);
        }
        catch (JsonException)
        {
            throw new AuditCaptureRejectedException(AuditActivityErrorCodes.IngestionFailed);
        }

        IReadOnlyList<string> problems = AuditCaptureRules.Problems(capture);
        return problems.Count > 0
            ? throw new AuditCaptureRejectedException(problems[0])
            : capture with { Attributes = AuditCaptureRules.Redact(capture.Attributes) };
    }

    private static string LockTimeoutStatement(TimeSpan timeout) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"SET LOCAL lock_timeout = '{(int)timeout.TotalMilliseconds}ms'");

    /// <summary>In a transaction of its own, after the failed one was rolled back.</summary>
    private async Task RecordFailureAsync(Guid messageId, string failure, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
        OutboxMessage message = await context.Set<OutboxMessage>().SingleAsync(m => m.Id == messageId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = timeProvider.GetUtcNow();
        AuditIngestionOptions settings = options.Value;
        message.AttemptCount++;
        double backOff = settings.RetryBaseDelay.TotalSeconds * Math.Pow(2, Math.Min(message.AttemptCount - 1, 20));
        message.NextAttemptAt = now + TimeSpan.FromSeconds(Math.Min(backOff, settings.MaxRetryDelay.TotalSeconds));
        message.LastError = failure;
        message.UpdatedAt = now;
        message.UpdatedBy = IngestionPrincipalId;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit message {MessageId} was not ingested: {Reason}. It stays in the outbox and is tried again after its back-off.")]
    private static partial void LogIngestionFailed(ILogger logger, Guid messageId, string reason);

    /// <summary>A capture refused at ingestion, with the FG-06 §22 code recorded as the message's last error.</summary>
    private sealed class AuditCaptureRejectedException(string code) : Exception(code)
    {
        public string Code { get; } = code;
    }
}

/// <summary>Configuration section <c>Audit:Ingestion</c>: how often and how much ingestion stores, and how it waits after a failure (TASK-073).</summary>
internal sealed class AuditIngestionOptions
{
    public const string Section = "Audit:Ingestion";

    /// <summary>How long the worker waits after a pass that found nothing more to store. Most messages are ingested at commit.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 100;

    /// <summary>The wait after a first failed attempt; each later failure doubles it, up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>How long one ingestion waits for a lock (the chain's, or a row an event's foreign key names) before it gives up and is retried.</summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
