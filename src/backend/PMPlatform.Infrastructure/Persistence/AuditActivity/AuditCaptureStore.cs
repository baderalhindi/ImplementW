using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>
/// Writes each captured audit event as an <c>AUDIT_EVENT</c> outbox message (TASK-073, M-6, ERD D-17). <see cref="Stage"/> uses the
/// request's context, so the producer's save commits the message with its change, and tells <see cref="AuditCommitInterceptor"/> to
/// ingest it once that save has committed. <see cref="RecordAsync"/> uses a context of its own, so a refusal is captured even though
/// nothing else in the request is, and nothing pending in the request is committed with it.
/// </summary>
internal sealed class AuditCaptureStore(
    PMPlatformDbContext context, DbContextOptions<PMPlatformDbContext> options, AuditCommitInterceptor commits) : IAuditCaptureStore
{
    public void Stage(AuditCapture capture)
    {
        OutboxMessage message = MessageOf(capture);
        context.Add(message);
        commits.IngestAfterCommit(context, message.Id);
    }

    public async Task RecordAsync(AuditCapture capture)
    {
        OutboxMessage message = MessageOf(capture);
        await using (PMPlatformDbContext own = new(options))
        {
            own.Add(message);

            // Not cancellable: an aborted request is audited all the same (IAuditTrail.RecordAsync). A failure here throws.
            await own.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await commits.IngestNowAsync([message.Id]).ConfigureAwait(false);
    }

    private static OutboxMessage MessageOf(AuditCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Guid author = capture.ActorUserId ?? AuditCapturePrincipal.Id;
        return new OutboxMessage
        {
            Id = capture.EventId,
            SourceModule = capture.SourceModule,
            MessageType = EventKind.AuditEvent,
            MessageKey = capture.MessageKey,
            Payload = capture.Serialize(),
            OccurredAt = capture.OccurredAt,
            CreatedAt = capture.OccurredAt,
            CreatedBy = author,
            UpdatedAt = capture.OccurredAt,
            UpdatedBy = author,
        };
    }
}
