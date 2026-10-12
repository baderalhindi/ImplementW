namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// Where a captured audit event is made durable (TASK-073, M-6): an <c>AUDIT_EVENT</c> message in <c>common.outbox_message</c>.
/// The authoritative store is written from it by ingestion, after the message has committed.
/// </summary>
public interface IAuditCaptureStore
{
    /// <summary>
    /// Adds the message to the request's unit of work, committed by the producer's next save with the change it reports, or
    /// not at all. Once that save commits, the event is ingested at once; what is not, ingestion's worker stores later.
    /// </summary>
    public void Stage(AuditCapture capture);

    /// <summary>
    /// Commits the message now in a unit of work of its own, independent of anything the request has pending, then ingests it.
    /// Throws if the message cannot be committed: the caller's request then fails rather than go unaudited (TASK-033 D-7). A
    /// failed ingestion does not throw: the message is durable and is stored on a later pass.
    /// </summary>
    public Task RecordAsync(AuditCapture capture);
}
