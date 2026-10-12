using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// The <c>audit_activity</c> rows a validated capture is stored as (FG-06 §4.2 STORED): the event, with the capture's id and the
/// digest of its attributes; its attributes; and a PENDING SIEM forwarding record if its class is forwarded (PTBC-029), in the
/// same unit of work, so it cannot be stored without being queued. The database adds the time it was recorded and its place in
/// the hash chain.
/// </summary>
public sealed class AuditRecordFactory(SiemForwardingPolicy forwarding)
{
    public AuditRecord Create(AuditCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        DateTimeOffset at = capture.OccurredAt;
        Guid recordedBy = capture.ActorUserId ?? AuditCapturePrincipal.Id;
        List<AuditEventAttribute> attributes = [.. capture.Attributes.Select(a => new AuditEventAttribute
        {
            Id = Guid.CreateVersion7(at),
            AuditEventId = capture.EventId,
            AttributeName = a.Name,
            OldValue = a.OldValue,
            NewValue = a.NewValue,
            CreatedAt = at,
            CreatedBy = recordedBy,
            UpdatedAt = at,
            UpdatedBy = recordedBy,
        })];

        AuditEvent auditEvent = new()
        {
            Id = capture.EventId,
            OccurredAt = at,
            EventClass = capture.EventClass,
            EventType = capture.EventType,
            ActorType = capture.ActorType,
            ActorUserId = capture.ActorUserId,
            SubjectModule = capture.Subject?.Module,
            SubjectType = capture.Subject?.Type,
            SubjectId = capture.Subject?.Id,
            ScopeProjectId = capture.ScopeProjectId,
            ScopeExternalEntityId = capture.ScopeExternalEntityId,
            CorrelationId = capture.CorrelationId,
            Outcome = capture.Outcome,
            DataClassificationItemId = capture.DataClassificationItemId,
            AttributeDigest = AuditAttributeDigest.Of(attributes),
            CreatedAt = at,
            CreatedBy = recordedBy,
            UpdatedAt = at,
            UpdatedBy = recordedBy,
        };

        return new AuditRecord(
            auditEvent,
            attributes,
            forwarding.Forwards(capture.EventClass)
                ? new AuditForwardingRecord
                {
                    Id = Guid.CreateVersion7(at),
                    AuditEventId = capture.EventId,
                    Status = AuditForwardingStatus.Pending,
                    CreatedAt = at,
                    CreatedBy = recordedBy,
                    UpdatedAt = at,
                    UpdatedBy = recordedBy,
                }
                : null);
    }
}
