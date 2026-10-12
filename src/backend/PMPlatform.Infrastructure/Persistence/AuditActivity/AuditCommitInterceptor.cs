using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.AuditActivity;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>
/// Ingests the audit messages a unit of work staged as soon as it has committed (TASK-073): after a save outside an explicit
/// transaction, or when the explicit transaction around it commits. The event is then in the store, and its Activity line in the
/// projection, before the request that caused it answers. Ingestion runs in units of work of its own and can only follow the
/// producer's commit, never undo it: a failure is logged and left to the ingestion worker, since the message is already durable.
/// Scoped with the request; it acts only for the context that staged the message, so a context the request opens beside its own
/// (e.g. <c>IAuditTrail.RecordAsync</c>) never takes another's messages.
/// </summary>
internal sealed partial class AuditCommitInterceptor(
    IAuditIngestor ingestor, IActivityProjectionRunner projection, ILogger<AuditCommitInterceptor> logger) : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly Dictionary<DbContext, List<Guid>> _staged = [];

    public void IngestAfterCommit(DbContext context, Guid messageId)
    {
        if (!_staged.TryGetValue(context, out List<Guid>? ids))
        {
            _staged[context] = ids = [];
        }

        ids.Add(messageId);
    }

    /// <summary>Ingests the messages and brings the Activity projection up to date, in units of work of their own.</summary>
    public async Task IngestNowAsync(IReadOnlyList<Guid> messageIds)
    {
        try
        {
            // Not cancellable: the producer has committed, so its evidence is stored whether or not its caller is still waiting.
            await ingestor.IngestAsync(messageIds, CancellationToken.None).ConfigureAwait(false);
            await projection.ProjectPendingAsync(CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The producer has committed; a failure here must not fail it. The worker stores the message later.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogIngestionDeferred(logger, messageIds.Count, exception.GetType().Name);
        }
    }

    public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context && context.Database.CurrentTransaction is null)
        {
            await FlushAsync(context).ConfigureAwait(false);
        }

        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context && context.Database.CurrentTransaction is null)
        {
            FlushAsync(context).GetAwaiter().GetResult();
        }

        return result;
    }

    public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            await FlushAsync(context).ConfigureAwait(false);
        }
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            FlushAsync(context).GetAwaiter().GetResult();
        }
    }

    private Task FlushAsync(DbContext context) =>
        _staged.Remove(context, out List<Guid>? ids) && ids.Count > 0 ? IngestNowAsync(ids) : Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} committed audit message(s) were not ingested at once: {Reason}. The ingestion worker stores them.")]
    private static partial void LogIngestionDeferred(ILogger logger, int count, string reason);
}
