using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// Sends each queued event and records the result on its forwarding record, one event at a time, so an event the SIEM
/// accepted is not sent again after a failure later in the batch. A refused or unreachable SIEM leaves the record FAILED,
/// and it is tried again on the next pass. The event row itself is never touched.
/// </summary>
internal sealed partial class SiemForwarder(
    IAuditForwardingRepository forwardings,
    ISiemClient siem,
    TimeProvider timeProvider,
    ILogger<SiemForwarder> logger) : ISiemForwarder
{
    public async Task<int> ForwardPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (!siem.IsConfigured)
        {
            return 0;
        }

        int forwarded = 0;
        foreach (PendingForwarding pending in await forwardings.FindUnforwardedAsync(batchSize, cancellationToken).ConfigureAwait(false))
        {
            bool accepted = await siem.SendAsync(SiemEventOf(pending), cancellationToken).ConfigureAwait(false);
            DateTimeOffset now = timeProvider.GetUtcNow();
            AuditForwardingRecord record = pending.Forwarding;
            record.Status = accepted ? AuditForwardingStatus.Forwarded : AuditForwardingStatus.Failed;
            record.ForwardedAt = accepted ? now : null;
            record.UpdatedAt = now;
            record.UpdatedBy = AuditTrail.AuditCapturePrincipalId;
            await forwardings.SaveAsync(cancellationToken).ConfigureAwait(false);

            if (!accepted)
            {
                // The SIEM is down or refusing: the rest of the batch would fail the same way.
                LogForwardingStopped(logger, pending.Event.Id);
                break;
            }

            forwarded++;
        }

        return forwarded;
    }

    internal static SiemEvent SiemEventOf(PendingForwarding pending)
    {
        AuditEvent e = pending.Event;
        return new SiemEvent(
            e.Id,
            AuditValue.Format(e.EventClass)!,
            e.EventType,
            AuditValue.Format(e.Outcome)!,
            e.OccurredAt,
            e.RecordedAt,
            new SiemActor(AuditValue.Format(e.ActorType)!, e.ActorUserId),
            e.SubjectModule is null && e.SubjectType is null && e.SubjectId is null ? null : new SiemSubject(e.SubjectModule, e.SubjectType, e.SubjectId),
            e.ScopeProjectId,
            e.ScopeExternalEntityId,
            e.CorrelationId,
            e.EventHash ?? throw new InvalidOperationException($"Audit event {e.Id} has no hash."),
            e.PreviousEventHash,
            [.. pending.Attributes.OrderBy(a => a.AttributeName, StringComparer.Ordinal).Select(a => new SiemAttribute(a.AttributeName, a.OldValue, a.NewValue))]);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SIEM forwarding stopped at audit event {AuditEventId}: the SIEM did not accept it. It is retried on the next pass.")]
    private static partial void LogForwardingStopped(ILogger logger, Guid auditEventId);
}
