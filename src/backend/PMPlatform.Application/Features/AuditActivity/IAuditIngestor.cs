namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// Moves committed <c>AUDIT_EVENT</c> messages into the authoritative audit store (FG-06 §4.2: RECEIVED → VALIDATED → STORED).
/// Each message is stored once, in a transaction that also marks it dispatched; a message that fails validation or storage
/// stays in the outbox with its error and is tried again after a back-off, indefinitely: evidence is never abandoned
/// (FAILED_INGESTION, AUD-CC-28).
/// </summary>
public interface IAuditIngestor
{
    /// <summary>Ingests the named messages now, in order; returns how many this call stored. Unknown or already ingested ids are skipped.</summary>
    public Task<int> IngestAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken);

    /// <summary>Ingests up to <paramref name="batchSize"/> messages that are due, oldest first; returns how many were stored.</summary>
    public Task<int> IngestDueAsync(int batchSize, CancellationToken cancellationToken);
}
